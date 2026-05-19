using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Services
{
    /// <summary>
    /// ============================================================
    /// SERVICIO: PrestamoNoPersonalService
    /// ------------------------------------------------------------
    /// Encapsula la lógica de préstamos NO personales para Estado
    /// de Cuenta.
    ///
    /// Flujo general:
    ///  1. Obtener préstamos vigentes del socio
    ///  2. Obtener tipos de préstamo aplicables
    ///  3. Construir filas visibles del Estado de Cuenta
    ///  4. Calcular alcance / puede solicitar
    ///  5. Calcular importe líquido
    ///
    /// Referencias VB replicadas:
    ///  - DameMenorAlcance
    ///  - ImporteLiquidoDePrestamo
    /// ============================================================
    /// </summary>
    public class PrestamoNoPersonalService : IPrestamoNoPersonalService
    {
        /* ============================================================
         * CAMPOS PRIVADOS
         * ============================================================ */

        private readonly Cmap54SistemasContext _context;
        private readonly IPrestamoCalculatorService _prestamoCalculatorService;

        /* ============================================================
         * CONSTRUCTOR
         * ============================================================ */

        public PrestamoNoPersonalService(
            Cmap54SistemasContext context,
            IPrestamoCalculatorService prestamoCalculatorService)
        {
            _context = context;
            _prestamoCalculatorService = prestamoCalculatorService;
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */

        public async Task<List<EstadoCuentaRowsDto>> GenerarPrestamosNoPersonalesAsync(
            EstadoCuentaContextDto contexto)
        {
            var prestamosVigentes = await ObtenerPrestamosVigentesAsync(
                contexto.ClavePension,
                contexto.FechaSistema);
                        
            var tiposPrestamo = await ObtenerTiposPrestamoAsync(
                contexto,
                prestamosVigentes);

            var resultado = new List<EstadoCuentaRowsDto>();

            foreach (var tipo in tiposPrestamo)
            {
                var fila = ConstruirFilaPorTipo(
                    contexto,
                    tipo,
                    prestamosVigentes);

                if (fila != null)
                    resultado.Add(fila);
            }

            AgregarFilasProyectadas(
                contexto,
                resultado,
                prestamosVigentes);

            return resultado
                .OrderBy(x => x.OrdenVisual)
                .ThenBy(x => x.ClavePrestamo == "PR" ? x.PlazoMeses : 0)
                .ThenBy(x => x.SubClave)
                .ThenBy(x => x.NombrePrestamo)
                .ToList();
        }

        /* ============================================================================
        * CONSTRUIR FILA POR TIPO DE PRÉSTAMO
        * ----------------------------------------------------------------------------
        * Este método replica la lógica VB de agregar un renglón al Estado de Cuenta.
        *
        * Responsabilidades:
        * 1. Buscar si el socio tiene préstamo vigente del tipo actual.
        * 2. Cargar datos reales del préstamo si existe.
        * 3. Decidir si se debe calcular proyección / alcance.
        * 4. Decidir si la fila debe mostrarse o no.
        *
        * Regla importante:
        * Calcular proyección NO es lo mismo que mostrar fila.
        *
        * Ejemplos:
        * - EV / PR pueden mostrarse por proyección.
        * - VI para activos/SNTE no proyecta fuera de temporada.
        * - VI sí debe mostrarse si tiene saldo negativo o devolución pendiente.
        * - GM / EX / PH / PC / PS no deben mostrarse vacíos.
        * ============================================================================ */
        private EstadoCuentaRowsDto? ConstruirFilaPorTipo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            List<PrestamoVigenteDto> vigentes)
        {
            /* ========================================================================
             * 1. FILTRAR PRÉSTAMOS VIGENTES DEL TIPO ACTUAL
             * ------------------------------------------------------------------------
             * Para PP se agrupan todos los personales.
             * Para los demás préstamos se respeta ClavePrestamo + SubCve.
             * Esto es importante para PR, VI, VA u otros préstamos con modalidades.
             * ======================================================================== */
            List<PrestamoVigenteDto> prestamosDelTipo;

            if (tipo.ClavePrestamo == "PP")
            {
                prestamosDelTipo = vigentes
                    .Where(p => p.TipoPrestamo == "PP")
                    .ToList();
            }
            else if (tipo.ClavePrestamo == "PV")
            {
                prestamosDelTipo = vigentes
                    .Where(p =>
                        p.TipoPrestamo == "PV" &&
                        (p.SubCve ?? 0) == (tipo.SubCve ?? 0))
                    .ToList();
            }
            else
            {
                prestamosDelTipo = vigentes
                    .Where(p =>
                        p.TipoPrestamo == tipo.ClavePrestamo &&
                        (p.SubCve ?? 0) == (tipo.SubCve ?? 0))
                    .ToList();
            }

            /* ========================================================================
             * 2. PP SOLO DEBE TOMAR EL MÁS RECIENTE
             * ------------------------------------------------------------------------
             * Si existen varios personales, para el estado de cuenta se toma el último.
             * ======================================================================== */
            if (tipo.ClavePrestamo == "PP" && prestamosDelTipo.Any())
            {
                prestamosDelTipo = prestamosDelTipo
                    .OrderByDescending(x => x.FechaPrestamo ?? DateTime.MinValue)
                    .Take(1)
                    .ToList();
            }

            /* ========================================================================
             * 3. ACUMULAR SALDO E IMPORTE
             * ------------------------------------------------------------------------
             * saldoTotal:
             * - Puede ser positivo: préstamo con saldo pendiente.
             * - Puede ser negativo: devolución pendiente por cobro de más.
             *
             * importeTotal:
             * - Importe original del pagaré vigente encontrado.
             * ======================================================================== */
            decimal saldoTotal = prestamosDelTipo.Sum(x => x.SaldoPrestamo);
            decimal importeTotal = prestamosDelTipo.Sum(x => x.ImportePagare);

            /* ========================================================================
             * 4. SELECCIONAR PRÉSTAMO PRINCIPAL
             * ------------------------------------------------------------------------
             * Se usa el más reciente para tomar fechas, plazo, liquidaCon, descuento,
             * subclave y demás datos visibles del estado de cuenta.
             * ======================================================================== */
            var prestamoPrincipal = prestamosDelTipo
                .OrderByDescending(x => x.FechaPrestamo ?? DateTime.MinValue)
                .FirstOrDefault();

            decimal liquidaCon = prestamoPrincipal?.LiquidaCon ?? 0m;
            DateTime? fechaPrestamo = prestamoPrincipal?.FechaPrestamo;
            DateTime? fechaVencimiento = prestamoPrincipal?.FechaVencimiento;

            /* ========================================================================
             * 5. DETERMINAR SI EXISTE PRÉSTAMO VIGENTE REAL
             * ------------------------------------------------------------------------
             * Se considera vigente real cuando existe préstamo y su saldo es positivo.
             *
             * Nota:
             * Si el saldo es negativo, no es "vigente" como adeudo, pero sí debe poder
             * mostrarse porque representa devolución pendiente.
             * ======================================================================== */
            bool estaVigente = prestamoPrincipal != null &&
                               prestamoPrincipal.SaldoPrestamo > 0;

            /* ========================================================================
            * 6. DECIDIR SI SE EJECUTA LA SECCIÓN DE ALCANCE / PROYECCIÓN
            * ------------------------------------------------------------------------
            * Esta decisión replica la bandera VB:
            *
            * realizarRutinasSeccionAlcance = True / False
            *
            * Importante:
            * - Esto NO decide si se muestra saldo real.
            * - SaldoPrestamo > 0 o SaldoPrestamo < 0 se muestra aparte.
            * - Esto solo decide si se calcula PuedeSolicitar, ImporteLiquido y Descuento.
            *
            * Ejemplos:
            * - ES proyecta como préstamo común ligado a ES/PC.
            * - PC no proyecta individualmente.
            * - PV proyecta según estatus/temporada.
            * - PE proyecta solo para activos si está vigente.
            * - EV / PR / RE / VI proyectan si el catálogo está vigente.
            * - GM / AU / VA / PS / PH no proyectan normalmente.
            * - EX proyecta solo si está vigente.
            * ======================================================================== */
            bool realizarRutinasSeccionAlcance =
                DebeRealizarRutinasSeccionAlcance(
                    ctx,
                    tipo,
                    prestamoPrincipal);

            if (realizarRutinasSeccionAlcance &&
                prestamoPrincipal != null &&
                prestamoPrincipal.SaldoPrestamo > 0 &&
                (tipo.ClaveRenovacion ?? "").Trim() == "1")
            {
               
                bool cumpleRenovacion = CumplePorcentajeRenovacion(
                    prestamoPrincipal,
                    tipo);

                if (!cumpleRenovacion)
                    realizarRutinasSeccionAlcance = false;
            }

            /* ========================================================================
             * 7. CALCULAR DESCUENTO SI HAY PRÉSTAMO VIGENTE
             * ------------------------------------------------------------------------
             * Equivale al AmortAnt del VB:
             *
             * Si saldo < amortización:
             *     descuento = saldo
             * Si no:
             *     descuento = importe amortización
             * ======================================================================== */
            decimal descuento = 0m;

            if (estaVigente && prestamoPrincipal != null)
            {
                descuento = _prestamoCalculatorService.CalcularDescuento(
                    prestamoPrincipal.SaldoPrestamo,
                    prestamoPrincipal.ImporteAmortizacion);
            }

            /* ========================================================================
            * 8. CALCULAR ALCANCE / PROYECCIÓN
            * ------------------------------------------------------------------------
            * Solo se ejecuta si realizarRutinasSeccionAlcance = true.
            * ======================================================================== */

            decimal puedeSolicitar = 0m;
            decimal importeLiquido = 0m;

            if (realizarRutinasSeccionAlcance)
            {
                (puedeSolicitar, importeLiquido) = CalcularAlcanceNoPersonal(
                    ctx,
                    tipo,
                    saldoTotal,
                    tipo.PlazoMaximo);
            }

            /* ========================================================================
            * 9. AJUSTAR IMPORTE LÍQUIDO CUANDO NO HAY ALCANCE
            * ------------------------------------------------------------------------
            * Si existe préstamo vigente pero NO se calculó alcance, el líquido queda en cero.
            *
            * Si sí existe puedeSolicitar, significa que la rutina de alcance corrió
            * y debe conservarse el importe líquido calculado.
            * ======================================================================== */
            bool tieneAlcanceCalculado = puedeSolicitar > 0;

            if (estaVigente && !tieneAlcanceCalculado)
                importeLiquido = 0m;

            /* ========================================================================
             * 10. DECIDIR SI LA FILA SE DEBE MOSTRAR
             * ------------------------------------------------------------------------
             * Este helper reemplaza los return null tempranos.
             *
             * Permite casos como:
             * - Viajes no proyecta, pero aparece si tiene saldo negativo.
             * - VA aparece si tiene liquidación negativa.
             * - GM / EX / PH no aparecen vacíos.
             * - EV / PR aparecen por proyección.
             * ======================================================================== */
            bool debeMostrar = DebeMostrarFilaPrestamo(
                ctx,
                tipo,
                prestamoPrincipal,
                saldoTotal,
                liquidaCon,
                puedeSolicitar);

            if (!debeMostrar)
                return null;

            /* ========================================================================
             * 11. DETERMINAR SUBCLAVE VISUAL
             * ------------------------------------------------------------------------
             * Si existe préstamo real, se usa su SubCve.
             * Si no, se usa la SubCve del catálogo.
             * ======================================================================== */
            int subClave = prestamoPrincipal?.SubCve ?? tipo.SubCve ?? 0;

            /* ========================================================================
             * 12. CONSTRUIR FILA FINAL DEL ESTADO DE CUENTA
             * ======================================================================== */
            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = subClave,
                NombrePrestamo = ObtenerNombreVisible(tipo.ClavePrestamo, subClave,tipo.NombrePrestamo),

                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importeTotal,
                PlazoMeses = prestamoPrincipal?.NumMesesPrestamo ?? tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,

                SaldoPrestamo = saldoTotal,
                CantidadPuedeSolicitar = puedeSolicitar,
                ImporteLiquido = importeLiquido,
                Descuento = descuento,
                LiquidaCon = liquidaCon,

                EstaVigente = estaVigente,
                EsProyeccion = realizarRutinasSeccionAlcance,
                OrdenVisual = ObtenerOrdenVisual(tipo.ClavePrestamo, subClave)
            };
        }

        /* ============================================================
         * SECCIÓN 2: CÁLCULO DE ALCANCE
         * ============================================================ */

        /* ============================================================
         * VB: DameMenorAlcance
         * ------------------------------------------------------------
         * Calcula cuánto puede solicitar el socio.
         *
         * Compara:
         *  1. Alcance por ahorro
         *  2. Alcance por sueldo / capacidad de pago
         *
         * Regla principal:
         *  - Se toma el menor alcance disponible.
         *
         * También valida:
         *  - Vigencia del tipo de préstamo
         *  - Meses mínimos cotizados
         *  - Número de pagos según estatus
         *  - Delegación al cálculo general o al cálculo de Eventos Sociales
         *
         * Caso especial EV EventosSociales:
         *  - EV replica lógica especial de VB.
         *  - Usa alcancePorSueldo como baseCalculo.
         *  - Recalcula puedeSolicitar sumando intereses, seguro y fondo.
         * ============================================================ */

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceNoPersonal(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal saldoActualDelTipo,
            int plazoMeses)
        {
            if (tipo.Vigente != "S")
                return (0m, 0m);

            if (tipo.MesesMinCotizados > 0 && ctx.MesesCot < tipo.MesesMinCotizados)
                return (0m, 0m);

            int numeroPagos = CalcularNumeroPagos(ctx, plazoMeses);

            if (numeroPagos <= 0)
                return (0m, 0m);

            decimal amortAnt = saldoActualDelTipo > 0 ? saldoActualDelTipo : 0m;

            decimal alcancePorAhorro = CalcularAlcancePorAhorro(ctx, tipo);

            decimal alcancePorSueldo = CalcularAlcancePorSueldo(
                ctx,
                tipo,
                amortAnt,
                numeroPagos);

            decimal puedeSolicitar = ObtenerMenorAlcance(
                alcancePorAhorro,
                alcancePorSueldo);

            if (tipo.ClavePrestamo == "EV")
            {
                return CalcularAlcanceEventosSociales(
                    ctx,
                    tipo,
                    saldoActualDelTipo,
                    numeroPagos,
                    alcancePorSueldo,
                    puedeSolicitar);
            }

            if (tipo.ClavePrestamo == "PR")
            {
                return CalcularAlcancePrendario(
                    ctx,
                    tipo,
                    numeroPagos,
                    saldoActualDelTipo);
            }

            if (tipo.ClavePrestamo == "RE")
            {
                return CalcularAlcanceRefaccionario(
                    ctx,
                    tipo,
                    numeroPagos,
                    saldoActualDelTipo);
            }

            if (tipo.ClavePrestamo == "PV")
            {
                return CalcularAlcanceViajes(
                    ctx,
                    tipo,
                    puedeSolicitar,
                    numeroPagos,
                    saldoActualDelTipo);
            }

            return CalcularAlcanceGeneralNoPersonal(
                ctx,
                tipo,
                puedeSolicitar,
                numeroPagos,
                saldoActualDelTipo);
        }

        //____________________________________________________________________________________________________
        private bool CumplePorcentajeRenovacion(
        PrestamoVigenteDto prestamo,
        TipoPrestamoDto tipo)
        {
            if (prestamo.ImportePagare <= 0)
                return false;

            decimal porcentajeCubierto =
                ((prestamo.ImportePagare - prestamo.SaldoPrestamo)
                / prestamo.ImportePagare) * 100m;

            return porcentajeCubierto >= tipo.PorcenRenova;
        }
        //------------------------------------------------------------------------------------------------------

        private decimal CalcularPorcentajeCubiertoRenovacion(
            PrestamoVigenteDto prestamo)
        {
            if (prestamo.ImportePagare <= 0)
                return 0m;

            if (prestamo.SaldoPrestamo < 0)
                return 100m;

            return Math.Round(
                100m - ((prestamo.SaldoPrestamo * 100m) / prestamo.ImportePagare),
                4);
        }

        //------------------------------------------------------------------------------------------------------

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceViajes(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal montoLiquidoObjetivo,
        int numeroPagos,
        decimal liquidaCon)
        {
            decimal alcancePorSueldoReal = CalcularAlcancePorSueldoSinTope(
                ctx,
                liquidaCon > 0 ? liquidaCon : 0m,
                numeroPagos);

            montoLiquidoObjetivo = tipo.MontoMaximo > 0
                ? Math.Min(alcancePorSueldoReal, tipo.MontoMaximo)
                : alcancePorSueldoReal;

            if (!ctx.EsSolicitudEspecial)
            {
                decimal topeGlobal = ctx.MisAhorros * 3.5m;

                decimal disponibleGlobal =
                    topeGlobal -
                    ctx.SaldoPrestamosTopadosAhorro +
                    liquidaCon;

                if (disponibleGlobal < 0)
                    disponibleGlobal = 0m;

                montoLiquidoObjetivo = Math.Min(montoLiquidoObjetivo, disponibleGlobal);
            }

            montoLiquidoObjetivo = Math.Round(Math.Max(0m, montoLiquidoObjetivo), 2);

            var resultado = CalcularPrestamoLiquidoLegacy(
                ctx,
                tipo,
                montoLiquidoObjetivo,
                numeroPagos,
                liquidaCon);

            return (
                resultado.PuedeSolicitar,
                resultado.ImporteLiquido
            );
        }

        private int CalcularNumeroPagos(
            EstadoCuentaContextDto ctx,
            int plazoMeses)
        {
            return ctx.Estatus == "A"
                ? plazoMeses * 2
                : plazoMeses;
        }

        /* ============================================================
        * Cálculo general para préstamos NO personales
        * ------------------------------------------------------------
        * Aplica:
        * - Tope global por ahorro (3.5x)
        * - Cálculo de importe líquido general
        *
        * Este flujo aplica para todos los préstamos excepto EV.
        * ============================================================ */

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceGeneralNoPersonal(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal puedeSolicitar,
        int numeroPagos,
        decimal saldoActualDelTipo)
        {
            if (!ctx.EsSolicitudEspecial)
            {
                decimal topeGlobal = ctx.MisAhorros * 3.5m;

                decimal disponibleGlobal =
                    topeGlobal -
                    ctx.SaldoPrestamosTopadosAhorro +
                    saldoActualDelTipo;

                if (disponibleGlobal < 0)
                    disponibleGlobal = 0m;

                puedeSolicitar = Math.Min(puedeSolicitar, disponibleGlobal);
            }

            if (puedeSolicitar < 0)
                puedeSolicitar = 0m;

            puedeSolicitar = Math.Round(puedeSolicitar, 2);

            decimal importeLiquido = CalcularImporteLiquidoPrestamo(
                ctx,
                tipo,
                puedeSolicitar,
                numeroPagos,
                saldoActualDelTipo);

            return (
                puedeSolicitar,
                Math.Round(importeLiquido, 2)
            );
        }

        private sealed class ResultadoPrestamoLiquidoLegacy
        {
            public decimal PuedeSolicitar { get; set; }
            public decimal ImporteLiquido { get; set; }
        }

        private ResultadoPrestamoLiquidoLegacy CalcularPrestamoLiquidoLegacy(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal montoLiquidoObjetivo,
            int numeroPagos,
            decimal liquidaCon)
        {
            decimal importeLiquido = montoLiquidoObjetivo - liquidaCon;

            if (importeLiquido < 0)
                importeLiquido = 0m;

            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            decimal intereses = CalcularInteresAPrestamo(
                montoLiquidoObjetivo,
                tasaPeriodo,
                numeroPagos);

            DateTime primerPago = ObtenerPrimerPago(ctx);

            int diasAdic = CalcularDiasAdicionales(
                ctx,
                tipo.ClavePrestamo,
                primerPago);

            intereses += CalcularInteresDiasAdicionales(
                tipo,
                montoLiquidoObjetivo,
                diasAdic);

            decimal seguro = 0m;

            if (tipo.PorcenSeguroPasivo > 0)
            {
                seguro = Math.Round(
                    (montoLiquidoObjetivo + intereses) *
                    (tipo.PorcenSeguroPasivo / 100m),
                    2);
            }

            decimal fondo = 0m;

            if (tipo.PorcenFondoGarantia > 0)
            {
                fondo = Math.Round(
                    (montoLiquidoObjetivo + intereses) *
                    (tipo.PorcenFondoGarantia / 100m),
                    2);

                if (fondo < 0)
                    fondo = 0m;
            }

            decimal puedeSolicitar =
                montoLiquidoObjetivo +
                intereses +
                seguro +
                fondo;

            return new ResultadoPrestamoLiquidoLegacy
            {
                PuedeSolicitar = Math.Round(puedeSolicitar, 2),
                ImporteLiquido = Math.Round(importeLiquido, 2)
            };
        }

         /* ============================================================
         * VB: DameMenorAlcance
         * ------------------------------------------------------------
         * Esta parte representa directamente la selección del menor
         * alcance entre:
         *
         *  - alcance por ahorro
         *  - alcance por sueldo
         *
         * Si no existe alcance por ahorro, se usa alcance por sueldo.
         * ============================================================ */

        private decimal ObtenerMenorAlcance(
            decimal alcancePorAhorro,
            decimal alcancePorSueldo)
        {
            return alcancePorAhorro > 0
                ? Math.Min(alcancePorAhorro, alcancePorSueldo)
                : alcancePorSueldo;
        }

        /* ============================================================
         * VB: Alcance por ahorro
         * ------------------------------------------------------------
         * Calcula el alcance basado en los ahorros del socio.
         *
         * Puede usar:
         *  - FactorSobreAhorro
         *  - VecesAhorro
         *
         * Al final aplica MontoMaximo si existe.
         * ============================================================ */

        private decimal CalcularAlcancePorAhorro(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo)
        {
            decimal alcance = 0m;

            if (tipo.FactorSobreAhorro > 0)
                alcance = ctx.MisAhorros * tipo.FactorSobreAhorro;
            else if (tipo.VecesAhorro > 0)
                alcance = ctx.MisAhorros * tipo.VecesAhorro;

            return AplicarMontoMaximo(alcance, tipo);
        }

        /* ============================================================
         * VB: Alcance por sueldo / capacidad de pago
         * ------------------------------------------------------------
         * Fórmula replicada:
         *
         *  sueldoDisponible = TotSueldo - ElLimite
         *  alcance = (sueldoDisponible + amortAnt) * numeroPagos
         *
         * Donde:
         *  - TotSueldo representa el sueldo considerado
         *  - ElLimite representa el mínimo que debe quedar libre
         *  - amortAnt representa la amortización/saldo anterior
         *
         * Al final aplica MontoMaximo si existe.
         * ============================================================ */

        private decimal CalcularAlcancePorSueldo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal amortAnt,
            int numeroPagos)
        {
            decimal sueldoDisponible = Math.Round(ctx.TotSueldo - ctx.ElLimite, 2);

            if (sueldoDisponible < 0)
                return 0m;

            decimal alcancePorSueldo = (sueldoDisponible + amortAnt) * numeroPagos;

            return AplicarMontoMaximo(alcancePorSueldo, tipo);
        }

        private decimal CalcularAlcancePorSueldoSinTope(
            EstadoCuentaContextDto ctx,
            decimal amortAnt,
            int numeroPagos)
        {
            decimal sueldoDisponible = Math.Round(ctx.TotSueldo - ctx.ElLimite, 2);

            if (sueldoDisponible < 0)
                return 0m;

            decimal alcancePorSueldo = (sueldoDisponible + amortAnt) * numeroPagos;

            return Math.Round(alcancePorSueldo, 2);
        }

        private decimal AplicarMontoMaximo(
            decimal alcance,
            TipoPrestamoDto tipo)
        {
            if (alcance < 0)
                return 0m;

            if (tipo.MontoMaximo > 0 && alcance > tipo.MontoMaximo)
                return tipo.MontoMaximo;

            return Math.Round(alcance, 2);
        }

        private decimal AplicarTopeGlobalPorAhorro(
            EstadoCuentaContextDto ctx,
            decimal puedeSolicitar,
            decimal saldoActualDelTipo)
        {
            decimal topeGlobal = ctx.MisAhorros * 3.5m;

            decimal disponibleGlobal =
                topeGlobal -
                ctx.SaldoPrestamosTopadosAhorro +
                saldoActualDelTipo;

            if (disponibleGlobal < 0)
                disponibleGlobal = 0m;

            return Math.Min(puedeSolicitar, disponibleGlobal);
        }

        private bool EsPrestamoSoloSiTieneMovimiento(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo)
        {
            // VIAJES:
            // Jubilados siempre pueden proyectar.
            // Activos/SNTE solo si tienen movimiento real.
            if (tipo.ClavePrestamo == "PV")
                return ctx.Estatus != "J";

            return tipo.ClavePrestamo switch
            {
                "EX" => true,
                "GM" => true,
                "AU" => true,
                "VA" => true,
                "PC" => true,
                "PS" => true,
                "PH" => true,

                _ => false
            };
        }

        private bool DebeMostrarFilaPrestamo(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        PrestamoVigenteDto? prestamoVigente,
        decimal saldoPrestamo,
        decimal liquidaCon,
        decimal puedeSolicitar)
        {
            string clave = tipo.ClavePrestamo;

            // PP se muestra desde PrestamoPersonalService.
            if (clave == "PP")
                return false;

            // ES siempre visible.
            if (clave == "ES")
                return true;

            // Saldo real o devolución real SIEMPRE se muestra.
            bool tieneSaldoReal =
                prestamoVigente != null &&
                saldoPrestamo != 0;

            bool tieneDevolucion =
                saldoPrestamo < 0 ||
                liquidaCon < 0;

            if (tieneSaldoReal || tieneDevolucion)
                return true;

            // Si no hay saldo real, solo se muestra si hay proyección.
            bool tieneProyeccion = puedeSolicitar > 0;

            return tieneProyeccion;
        }

        /* ============================================================================
        * DETERMINAR SI EL TIPO DE PRÉSTAMO DEBE CALCULAR PROYECCIÓN
        * ----------------------------------------------------------------------------
        * Replica la lógica VB de:
        *
        * realizarRutinasSeccionAlcance = True / False
        *
        * Reglas:
        *
        * 1. Este método solo decide si corre la sección de alcance/proyección.
        *
        * 2. La existencia de préstamo vigente NO oculta la fila.
        *    El saldo, fecha, importe y liquidación se cargan aparte.
        *
        * 3. Más adelante, si el préstamo vigente cumple porcentaje de renovación,
        *    aquí se permitirá proyectar aun con saldo vigente.
        *
        * 4. GM / EX / PH / PC / PS:
        *    -> No proyectan normalmente.
        *
        * 5. EV / PR / RE / CO:
        *    -> Sí proyectan si están vigentes en catálogo.
        * ============================================================================ */
        private bool DebeRealizarRutinasSeccionAlcance(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            PrestamoVigenteDto? prestamoVigente)
        {
            string clave = tipo.ClavePrestamo;

            /* ============================================================
             * PP se procesa desde PrestamoPersonalService.
             * ============================================================ */
            if (clave == "PP")
                return false;

            /* ============================================================
             * ES siempre proyecta.
             * ES es quien controla el grupo ES/PC.
             * ============================================================ */
            if (clave == "ES")
                return true;

            /* ============================================================
             * PC nunca proyecta individualmente.
             * Solo se muestra si tiene saldo vigente.
             * ============================================================ */
            if (clave == "PC")
                return false;

            // EX no proyecta normalmente.
            // Solo debe mostrarse si tiene saldo real/devolución.
            if (clave == "EX")
                return false;

            /* ============================================================
             * GM / AU / VA / PS / PH
             * Nunca proyectan normalmente.
             * Solo muestran movimiento/saldo.
             * ============================================================ */
            if (clave is "GM" or "AU" or "VA" or "PS" or "PH")
                return false;

            /* ============================================================
             * PV - VIAJES
             * ------------------------------------------------------------
             * Jubilados:
             *     siempre proyectan.
             *
             * Activos/SNTE:
             *     solo si está vigente.
             * ============================================================ */
            if (clave == "PV")
            {
                if (ctx.Estatus == "J")
                    return true;

                return tipo.Vigente == "S";
            }

            /* ============================================================
             * PE - PREPARACIÓN PROFESIONAL
             * ------------------------------------------------------------
             * Jubilados:
             *     nunca proyectan.
             *
             * Activos/SNTE:
             *     solo si está vigente.
             * ============================================================ */
            if (clave == "PE")
            {
                if (ctx.Estatus == "J")
                    return false;

                return tipo.Vigente == "S";
            }

            /* ============================================================
             * REGLA GENERAL
             * ------------------------------------------------------------
             * EV / PR / RE / VI / etc.
             * ============================================================ */
            return tipo.Vigente == "S";
        }


        /* ============================================================
         * SECCIÓN 3: CASOS ESPECIALES DE ALCANCE
         * ============================================================ */

        /* ============================================================
         * VB: Caso especial EV
         * ------------------------------------------------------------
         * EV no se comporta igual que los préstamos normales.
         *
         * Lógica replicada:
         *  1. Se obtiene el importe líquido inicial:
         *      importeLiquidoEv = puedeSolicitar - saldoActualDelTipo
         *
         *  2. Se usa alcancePorSueldo como base del cálculo.
         *
         *  3. Se calculan:
         *      - intereses
         *      - seguro pasivo
         *      - fondo de garantía
         *
         *  4. PuedeSolicitar se recalcula:
         *      baseCalculo + intereses + seguro + fondo
         *
         * Pendiente:
         *  - Integrar DiasAdic para cerrar diferencia contra VB.
         * ============================================================ */

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceEventosSociales(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal saldoActualDelTipo,
        int numeroPagos,
        decimal alcancePorSueldo,
        decimal puedeSolicitarInicial)
        {
            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            /*
             * En EV, igual que PR con EsLiquido = "S",
             * la base real del cálculo es el importe líquido objetivo.
             */
            decimal importeLiquidoEv = alcancePorSueldo;

            decimal interesesEv = CalcularInteresAPrestamo(
                importeLiquidoEv,
                tasaPeriodo,
                numeroPagos);

            DateTime primerPago = ObtenerPrimerPago(ctx);

            int diasAdicEv = CalcularDiasAdicionales(
                ctx,
                tipo.ClavePrestamo,
                primerPago);

            decimal interesesDiasAdic = CalcularInteresDiasAdicionales(
                tipo,
                importeLiquidoEv,
                diasAdicEv);

            decimal interesesTotalesEv = interesesEv + interesesDiasAdic;

            decimal seguroEv = CalcularSeguroPasivo(
                importeLiquidoEv,
                interesesTotalesEv,
                tipo);

            decimal fondoEv = CalcularFondoGarantia(
                importeLiquidoEv,
                interesesTotalesEv,
                tipo);

            decimal puedeSolicitarEv = Math.Round(
                importeLiquidoEv
                + interesesTotalesEv
                + seguroEv
                + fondoEv,
                2);

            return (
                puedeSolicitarEv,
                importeLiquidoEv
            );
        }

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcancePrendario(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        int numeroPagos,
        decimal saldoActualDelTipo)
        {
            decimal importeLiquidoObjetivo = tipo.MontoMaximo;

            if (importeLiquidoObjetivo <= 0)
                return (0m, 0m);

            decimal puedeSolicitar = CalcularPuedeSolicitarDesdeLiquidoPrendario(
                ctx,
                tipo,
                importeLiquidoObjetivo,
                numeroPagos,
                saldoActualDelTipo);

            return (
                Math.Round(puedeSolicitar, 2),
                Math.Round(importeLiquidoObjetivo, 2)
            );
        }

        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceRefaccionario(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        int numeroPagos,
        decimal saldoActualDelTipo)
        {
            decimal importeLiquido = tipo.MontoMaximo;

            if (importeLiquido <= 0)
                return (0m, 0m);

            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            decimal intereses = CalcularInteresAPrestamo(
                importeLiquido,
                tasaPeriodo,
                numeroPagos);

            DateTime primerPago = ObtenerPrimerPago(ctx);

            int diasAdic = CalcularDiasAdicionales(
                ctx,
                tipo.ClavePrestamo,
                primerPago);

            intereses += CalcularInteresDiasAdicionales(
                tipo,
                importeLiquido,
                diasAdic);

            decimal seguro = CalcularSeguroPasivo(
                importeLiquido,
                intereses,
                tipo);

            decimal fondo = CalcularFondoGarantia(
                importeLiquido,
                intereses,
                tipo);

            decimal puedeSolicitar =
                importeLiquido +
                intereses +
                seguro +
                fondo;

            decimal importeLiquidoReal = importeLiquido - saldoActualDelTipo;

            if (importeLiquidoReal < 0)
                importeLiquidoReal = 0m;

            return (
                Math.Round(puedeSolicitar, 2),
                Math.Round(importeLiquidoReal, 2)
            );
        }

        /* ============================================================
         * SECCIÓN 4: IMPORTE LÍQUIDO
         * ============================================================ */

        /* ============================================================
         * VB: ImporteLiquidoDePrestamo
         * ------------------------------------------------------------
         * Calcula el dinero real que recibe el socio.
         *
         * Considera:
         *  - Capital solicitado
         *  - Intereses
         *  - Seguro pasivo
         *  - Fondo de garantía
         *  - Saldo anterior del préstamo
         *
         * Fórmula basada en amortización nivelada.
         * ============================================================ */

        private decimal CalcularImporteLiquidoPrestamo(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal capital,
        int numeroPagos,
        decimal saldoPrestamo)
        {
            if (capital <= 0 || numeroPagos <= 0)
                return 0m;

            // ============================================================
            // CASO ESPECIAL VB: ESPECIAL / ES
            // ES no usa la fórmula general de ImporteLiquidoPrestamo.
            // VB calcula:
            // 1) Quita seguro/fondo
            // 2) Quita interés normal
            // 3) Resta saldo anterior si aplica
            // ============================================================
            if (tipo.ClavePrestamo == "ES")
            {
                return CalcularImporteLiquidoEspecialES(
                    ctx,
                    tipo,
                    capital,
                    saldoPrestamo
                );
            }

            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            if (tasaPeriodo <= 0)
                return Math.Max(0m, Math.Round(capital - saldoPrestamo, 2));

            decimal tasaElevada = TasaElevada(tasaPeriodo, numeroPagos);

            decimal factorSeguroFondo =
                1m +
                (tipo.PorcenSeguroPasivo / 100m) +
                (AplicaFondoGarantia(tipo) ? tipo.PorcenFondoGarantia / 100m : 0m);

            decimal solicitadoSinSeguro = Math.Round(capital / factorSeguroFondo, 2);

            decimal amortizacionParcial = solicitadoSinSeguro / numeroPagos;

            decimal liquidoBruto = Math.Round(
                (amortizacionParcial * (tasaElevada - 1m)) /
                (tasaPeriodo * tasaElevada),
                2);

            decimal bonificaSeguroPasivo = 0m;
            decimal bonificaIntereses = 0m;
            decimal interesesMoratorios = 0m;

            decimal importeLiquido =
                liquidoBruto -
                (saldoPrestamo - bonificaSeguroPasivo - bonificaIntereses - interesesMoratorios);

            if (importeLiquido < 0)
                importeLiquido = 0m;

            return Math.Round(importeLiquido, 2);
        }

        private decimal CalcularImporteLiquidoEspecialES(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal puedeSolicitar,
        decimal saldoPrestamo)
        {
            decimal factorSeguroFondo =
                1m
                + (tipo.PorcenSeguroPasivo / 100m)
                + (AplicaFondoGarantia(tipo) ? tipo.PorcenFondoGarantia / 100m : 0m);

            decimal importeLiquido = Math.Round(puedeSolicitar / factorSeguroFondo, 2);

            importeLiquido = Math.Round(
                importeLiquido / (1m + (tipo.TasaIntNormal / 400m)),
                2
            );

            importeLiquido -= saldoPrestamo;

            if (importeLiquido < 0)
                importeLiquido = 0m;

            return Math.Round(importeLiquido, 2);
        }

        /* ============================================================
        * VB: ImporteLiquidoDePrestamo - Eventos Sociales EV
        * ------------------------------------------------------------
        * Para EV, el importe líquido se calcula diferente al flujo
        * general.
        *
        * Fórmula actual replicada:
        *
        * importeLiquido = puedeSolicitarInicial - saldoActualDelTipo
        *
        * Donde:
        * - puedeSolicitarInicial viene de DameMenorAlcance
        * - saldoActualDelTipo es el saldo vigente del préstamo EV
        *
        * Si el resultado es negativo, se regresa 0.
        * ============================================================ */

        private decimal CalcularImporteLiquidoEventosSociales(
            decimal puedeSolicitarInicial,
            decimal saldoActualDelTipo)
        {
            decimal importeLiquido = puedeSolicitarInicial - saldoActualDelTipo;

            if (importeLiquido < 0)
                importeLiquido = 0m;

            return Math.Round(importeLiquido, 2);
        }

        private decimal CalcularPuedeSolicitarDesdeLiquidoPrendario(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        decimal liquidoObjetivo,
        int numeroPagos,
        decimal saldoActualDelTipo)
        {
            decimal importeLiquido = liquidoObjetivo;

            decimal tasaPeriodo = (tipo.TasaIntNormal / 100m) / 24m;

            decimal intereses = CalcularInteresAPrestamo(
                importeLiquido,
                tasaPeriodo,
                numeroPagos);

            DateTime primerPago = ObtenerPrimerPago(ctx);

            int diasAdicPr = CalcularDiasAdicionales(
                ctx,
                tipo.ClavePrestamo,
                primerPago);

            intereses += CalcularInteresDiasAdicionales(
                tipo,
                importeLiquido,
                diasAdicPr);


            decimal baseSeguroFondo = importeLiquido + intereses;

            decimal seguro =
                baseSeguroFondo * (tipo.PorcenSeguroPasivo / 100m);

            decimal fondo =
                baseSeguroFondo * (tipo.PorcenFondoGarantia / 100m);

            decimal puedeSolicitar =
                importeLiquido
                + intereses
                + seguro
                + fondo;

            return Math.Round(puedeSolicitar, 2);
        }

        /* ============================================================
         * SECCIÓN 5: INTERESES, SEGURO Y FONDO
         * ============================================================ */

            /* ============================================================
             * VB: Cálculo de intereses del préstamo
             * ------------------------------------------------------------
             * Usa fórmula de pago nivelado:
             *
             *  pago = P * r * (1 + r)^n / ((1 + r)^n - 1)
             *
             *  intereses = totalPagado - capital
             *
             * IMPORTANTE:
             *  - VB redondea el pago nivelado a 2 decimales.
             * ============================================================ */

        private decimal CalcularInteresAPrestamo(
            decimal importePrestamo,
            decimal tasaPeriodo,
            int numeroPagos)
        {
            if (importePrestamo <= 0 || tasaPeriodo <= 0 || numeroPagos <= 0)
                return 0m;

            importePrestamo = Math.Round(importePrestamo, 2);

            decimal factor = TasaElevada(tasaPeriodo, numeroPagos);

            decimal pagoNivelado =
                importePrestamo *
                tasaPeriodo *
                factor /
                (factor - 1m);

            pagoNivelado = Math.Round(pagoNivelado, 2);

            decimal totalPagado = pagoNivelado * numeroPagos;

            decimal intereses = totalPagado - importePrestamo;

            return Math.Round(intereses, 2);
        }

        private decimal ObtenerTasaPeriodo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo)
        {
            return ctx.Estatus == "A"
                ? tipo.TasaIntNormal / 2400m
                : tipo.TasaIntNormal / 1200m;
        }

        private decimal TasaElevada(
            decimal tasaPeriodo,
            int numeroPagos)
        {
            return (decimal)Math.Pow((double)(1m + tasaPeriodo), numeroPagos);
        }

        private decimal CalcularSeguroPasivo(
            decimal baseCalculo,
            decimal intereses,
            TipoPrestamoDto tipo)
        {
            if (tipo.PorcenSeguroPasivo <= 0)
                return 0m;

            return Math.Round(
                (baseCalculo + intereses) * (tipo.PorcenSeguroPasivo / 100m),
                2);
        }

        private decimal CalcularFondoGarantia(
            decimal baseCalculo,
            decimal intereses,
            TipoPrestamoDto tipo)
        {
            if (!AplicaFondoGarantia(tipo))
                return 0m;

            return Math.Round(
                (baseCalculo + intereses) * (tipo.PorcenFondoGarantia / 100m),
                2);
        }

        private bool AplicaFondoGarantia(
            TipoPrestamoDto tipo)
        {
            return tipo.PorcenFondoGarantia > 0;
        }

        /* ============================================================
        * VB: PrimerPago real para DiasAdic
        * ------------------------------------------------------------
        * Obtiene la fecha real del primer pago según tipo de socio.
        *
        * Regla:
        * - Jubilado: FechaJub / FecProxJub
        * - Activo: FechaActivo / FecProxActivos
        * - SNTE/Empleado: FechaSnte / FecProxSnte
        *
        * IMPORTANTE:
        * La base oficial sigue siendo ctx.FechaSistema.
        * ============================================================ */
        private DateTime ObtenerPrimerPago(EstadoCuentaContextDto ctx)
        {
            DateTime? primerPago = ctx.Estatus switch
            {
                "J" => ctx.FecProxJub ?? ctx.FechaJub,
                "A" => ctx.FecProxActivos ?? ctx.FechaActivo,
                "S" => ctx.FecProxSnte ?? ctx.FechaSnte,
                _ => null
            };

            if (primerPago.HasValue)
                return primerPago.Value.Date;

            return ctx.FechaSistema.Date;
        }

        /* ============================================================
        * VB: DiasAdic - Días adicionales de préstamos
        * ------------------------------------------------------------
        * Calcula los días adicionales entre FechaSistema y PrimerPago,
        * replicando la lógica legacy VB:
        *
        * - Jubilados: base de 30 días
        * - Activos / Empleados: base de 15 días
        * - EX: no aplica
        *
        * Luego CalcularInteresDiasAdicionales convierte esos días
        * en interés diario proporcional.
        * ============================================================ */

        private int CalcularDiasAdicionales(
            EstadoCuentaContextDto ctx,
            string clavePrestamo,
            DateTime primerPago)
        {
            if ((clavePrestamo ?? "").Trim() == "EX")
                return 0;

            DateTime fechaSistema = ctx.FechaSistema.Date;

            int diasBase = ctx.Estatus == "J"
                ? 30
                : 15;

            if ((primerPago.Date - fechaSistema.AddDays(1)).Days > diasBase)
            {
                return (primerPago.Date.AddDays(-diasBase) - fechaSistema).Days;
            }

            return 0;
        }

        private decimal CalcularInteresDiasAdicionales(
            TipoPrestamoDto tipo,
            decimal baseCalculo,
            int diasAdic)
        {
            if (diasAdic <= 0 || baseCalculo <= 0 || tipo.TasaIntNormal <= 0)
                return 0m;

            decimal tasaAnual = tipo.TasaIntNormal / 100m;

            return Math.Round(baseCalculo * tasaAnual / 360m * diasAdic, 2);
        }


        /* ============================================================
         * SECCIÓN 6: TIPOS DE PRÉSTAMO
         * ============================================================ */

        private async Task<List<TipoPrestamoDto>> ObtenerTiposPrestamoAsync(
            EstadoCuentaContextDto ctx,
            List<PrestamoVigenteDto> vigentes)
        {
            var query =
                from tp in _context.TABLA_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                join dp in _context.DETALLE_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                    on tp.ClavePrestamo equals dp.ClavePrestamo
                where dp.TipoSocio == ctx.Estatus
                      && dp.Vigencia == ctx.Vigencia
                      && tp.ClavePrestamo != "IC"
                select new TipoPrestamoDto
                {
                    ClavePrestamo = tp.ClavePrestamo ?? string.Empty,
                    NombrePrestamo = !string.IsNullOrWhiteSpace(dp.NombrePrestamo)
                        ? dp.NombrePrestamo.Trim()
                        :(tp.NombrePrestamo ?? string.Empty).Trim(),

                    VecesAhorro = tp.VecesAhorro ?? 0,
                    PorcenRenova = tp.PorcenRenova ?? 0,
                    EsLiquido = tp.Esliquido,
                    ClaveRenovacion = tp.ClaveRenovacion,
                    PlazoRenovar = tp.PlazoRenovar ?? 0,

                    SubCve = dp.SubCve,
                    Vigente = dp.Vigente,
                    PlazoMaximo = dp.PlazoMaximo ?? 0,
                    TasaIntNormal = dp.TasaIntNormal ?? 0,
                    MontoMaximo = dp.MontoMaximo ?? 0,
                    PorcenSeguroPasivo = dp.PorcenSeguroPasivo ?? 0,
                    PorcenFondoGarantia = dp.PorcenFondoGarantia ?? 0,
                    FactorSobreAhorro = dp.FactorSobreAhorro ?? 0,
                    MesesMinCotizados = dp.MesesMinCotizados ?? 0
                };

            if (ctx.SoloPrestamoGM)
            {
                query = query.Where(x => x.ClavePrestamo == "GM");
            }

            var lista = await query.ToListAsync();

            bool tieneEsVigente = vigentes.Any(x =>
                x.TipoPrestamo == "ES" &&
                x.SaldoPrestamo > 0);

            bool tienePcVigente = vigentes.Any(x =>
                x.TipoPrestamo == "PC" &&
                x.SaldoPrestamo > 0);

            var tipoEs = lista.FirstOrDefault(x => x.ClavePrestamo == "ES");
            var tipoPc = lista.FirstOrDefault(x => x.ClavePrestamo == "PC");

            lista = lista
                .Where(x =>
                    x.ClavePrestamo != "ES" &&
                    x.ClavePrestamo != "PC")
                .ToList();

            if (tieneEsVigente && tipoEs != null)
            {
                lista.Add(tipoEs);
            }
            else if (tienePcVigente && tipoPc != null)
            {
                lista.Add(tipoPc);
            }
            else if (tipoEs != null)
            {
                lista.Add(tipoEs);
            }
            else if (tipoPc != null)
            {
                lista.Add(tipoPc);
            }

            var orden = new[] { "ES", "PC", "EV", "PR", "RE", "PV", "PE", "VI", "VA", "GM", "EX", "PH" };

            return lista
                .Where(x =>
                    orden.Contains(x.ClavePrestamo) &&
                    x.ClavePrestamo != "PP")
                .OrderBy(x => Array.IndexOf(orden, x.ClavePrestamo))
                .ThenBy(x => x.ClavePrestamo == "PR" ? x.PlazoMaximo : 0)
                .ThenBy(x => x.SubCve)
                .ThenBy(x => x.NombrePrestamo)
                .ToList();
        }

        /* ============================================================
         * SECCIÓN 7: PRÉSTAMOS VIGENTES
         * ============================================================ */

        private async Task<List<PrestamoVigenteDto>> ObtenerPrestamosVigentesAsync(
            string clavePension,
            DateTime fechaSistema)
        {
            var rows = await ObtenerPrestamosVigentesSqlAsync(
                clavePension,
                fechaSistema);

            var resultado = new List<PrestamoVigenteDto>(rows.Count);

            foreach (var row in rows)
            {
                decimal liquidaCon = 0m;
                decimal saldoPrestamo = row.SaldoPrestamo;

                if (row.SaldoPrestamo < 0)
                {
                    // Devolución / saldo a favor real
                    liquidaCon = Math.Round(row.SaldoPrestamo, 2);
                    saldoPrestamo = Math.Round(row.SaldoPrestamo, 2);
                }
                else if (row.SaldoPrestamo != 0)
                {
                    // Préstamo vigente normal
                    liquidaCon = await _prestamoCalculatorService.ObtenerLiquidaConAsync(
                        row.Id,
                        fechaSistema);
                }

                resultado.Add(new PrestamoVigenteDto
                {
                    Id = row.Id,
                    TipoPrestamo = row.TipoPrestamo,
                    SubCve = row.SubCve,
                    EstatusPrestamo = row.EstatusPrestamo,
                    SaldoPrestamo = saldoPrestamo,
                    ImportePagare = row.ImportePagare,
                    ImporteAmortizacion = row.ImporteAmortizacion,
                    NumMesesPrestamo = row.NumMesesPrestamo,
                    NumeroPagare = row.NumeroPagare,
                    FechaPrestamo = row.FechaPrestamo,
                    FechaVencimiento = row.FechaVencimiento,
                    LiquidaCon = liquidaCon
                });
            }

            return resultado;
        }

        private async Task<List<PrestamoVigenteSqlRow>> ObtenerPrestamosVigentesSqlAsync(
            string clavePension,
            DateTime fechaSistema)
        {
            return await _context.Set<PrestamoVigenteSqlRow>()
                .FromSqlInterpolated($@"
                    SELECT
                        tp.ID as Id,
                        tp.TipoPrestamo,
                        tp.SUBCVE as SubCve,
                        ISNULL(tp.EstatusPrestamo, '') as EstatusPrestamo,
                        ISNULL(tp.SaldoPrestamo,0) as SaldoPrestamo,
                        ISNULL(tp.ImportePagare,0) as ImportePagare,
                        ISNULL(tp.ImporteAmortizacion,0) as ImporteAmortizacion,
                        ISNULL(tp.NumMesesPrestamo,0) as NumMesesPrestamo,
                        ISNULL(tp.NumeroPagare,0) as NumeroPagare,
                        tp.FechaPrestamo,
                        tp.FechaVencimiento,
                        CAST(0 AS DECIMAL(18,2)) AS LiquidaCon
                    FROM TABLA_DE_PRESTAMOS tp
                    WHERE tp.ClavePension = {clavePension}
                    AND (
                        tp.EstatusPrestamo = 'VI'
                        OR (
                            tp.EstatusPrestamo = 'LI'
                            AND ISNULL(tp.ImporteAmortizacion, 0) > 0
                        )
                    )
                    ORDER BY tp.TipoPrestamo, tp.FechaPrestamo DESC
                ")
                .AsNoTracking()
                .ToListAsync();
        }

        /* ============================================================
         * SECCIÓN 8: PROYECCIONES TEMPORALES
         * ============================================================
         * Temporalmente desactivado.
         *
         * Motivo:
         * Las proyecciones hardcodeadas generaban duplicados e importes
         * incorrectos en Estado de Cuenta.
         *
         * Siguiente etapa:
         * Generar estas filas desde:
         *  - TABLA_DE_TIPOS_DE_PRESTAMOS
         *  - DETALLE_DE_TIPOS_DE_PRESTAMOS
         * ============================================================ */

        private void AgregarFilasProyectadas(
            EstadoCuentaContextDto ctx,
            List<EstadoCuentaRowsDto> resultado,
            List<PrestamoVigenteDto> vigentes)
        {
            // Intencionalmente vacío por ahora.
        }

        /* ============================================================
         * SECCIÓN 9: PRESENTACIÓN
         * ============================================================ */

        private string ObtenerNombreVisible(
        string clavePrestamo,
        int? subClave,
        string? nombreCatalogo)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", _) => "ESPECIAL",
                ("PC", _) => "COMPLEMENTARIO",
                ("EV", _) => "EVENTOS SOCIALES",

                ("PR", 1) => "PRENDARIO NORMAL",
                ("PR", 2) => "PRENDARIO TIPO A",
                ("PR", 3) => "PRENDARIO TIPO B",

                ("PR", _) => !string.IsNullOrWhiteSpace(nombreCatalogo)
                    ? nombreCatalogo.Trim()
    :               "PRENDARIO",

                ("RE", _) => "REFACCIONARIO",
                ("PV", _) => "VIAJES T.",
                ("PE", _) => "PREPARACIÓN PROFESIONAL",

                ("VA", _) => "PRESTAMO VARIOS",

                // EX no debe proyectar; si algún día aparece por saldo real,
                // conserva el nombre del catálogo.
                _ => !string.IsNullOrWhiteSpace(nombreCatalogo)
                    ? nombreCatalogo.Trim()
                    : clavePrestamo
            };
        }

        private int ObtenerOrdenVisual(string clavePrestamo, int subClave)
        {
            return clavePrestamo switch
            {
                "ES" => 10,
                "PC" => 10,

                "EV" => 20,

                // PERSONAL
                "PP" => 30,

                // PRENDARIOS
                "PR" => 40,

                // REFACCIONARIO
                "RE" => 50,

                // VIAJES
                "PV" => 60,

                "VA" => 70,
                "VI" => 75,
                "GM" => 80,
                "EX" => 90,
                "PH" => 100,

                _ => 999
            };
        }
    }
}