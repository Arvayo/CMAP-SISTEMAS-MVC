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
            // ============================================================
            // PP se procesa exclusivamente en PrestamoPersonalService
            // ============================================================
            if (tipo.ClavePrestamo == "PP")
                return null;

            // ============================================================
            // 1. OBTENER DATOS DEL PRÉSTAMO
            // ============================================================
            var datosPrestamo = ObtenerDatosPrestamo(
                tipo,
                vigentes);

            // ============================================================
            // 2. EVALUAR PROYECCIÓN / ALCANCE
            // ============================================================
            var alcance = EvaluarAlcancePrestamo(
                ctx,
                tipo,
                datosPrestamo);

            // ============================================================
            // 3. CALCULAR DESCUENTO VIGENTE
            // ============================================================
            decimal descuento = CalcularDescuentoPrestamo(
                datosPrestamo);

            // ============================================================
            // 4. AJUSTAR LÍQUIDO SI NO HUBO ALCANCE
            // ============================================================
            if (datosPrestamo.EstaVigente &&
                alcance.PuedeSolicitar <= 0)
            {
                alcance.ImporteLiquido = 0m;
            }

            // ============================================================
            // 5. VALIDAR SI LA FILA DEBE MOSTRARSE
            // ============================================================
            bool mostrar = DebeMostrarFilaPrestamo(
                ctx,
                tipo,
                datosPrestamo.PrestamoPrincipal,
                datosPrestamo.SaldoTotal,
                datosPrestamo.LiquidaCon,
                alcance.PuedeSolicitar);

            if (!mostrar)
                return null;

            // ============================================================
            // 6. DETERMINAR SUBCLAVE VISUAL
            // ============================================================
            int subClave =
                datosPrestamo.PrestamoPrincipal?.SubCve
                ?? tipo.SubCve
                ?? 0;

            // ============================================================
            // 7. CONSTRUIR FILA FINAL
            // ============================================================
            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = subClave,

                NombrePrestamo = ObtenerNombreVisible(
                    tipo.ClavePrestamo,
                    subClave,
                    tipo.NombrePrestamo),

                FechaPrestamo = datosPrestamo.FechaPrestamo,
                ImportePrestamo = datosPrestamo.ImporteTotal,

                PlazoMeses =
                    datosPrestamo.PrestamoPrincipal?.NumMesesPrestamo
                    ?? tipo.PlazoMaximo,

                FechaVencimiento = datosPrestamo.FechaVencimiento,

                SaldoPrestamo = datosPrestamo.SaldoTotal,

                CantidadPuedeSolicitar = alcance.PuedeSolicitar,
                ImporteLiquido = alcance.ImporteLiquido,

                Descuento = descuento,
                LiquidaCon = datosPrestamo.LiquidaCon,

                EstaVigente = datosPrestamo.EstaVigente,
                EsProyeccion = alcance.RealizarRutinasSeccionAlcance,

                OrdenVisual = ObtenerOrdenVisual(
                    tipo.ClavePrestamo,
                    subClave)
            };
        }

        /* ============================================================================
        * DTO INTERNO: DatosPrestamoNoPersonal
        * ----------------------------------------------------------------------------
        * Agrupa los datos reales encontrados para el tipo de préstamo actual.
        *
        * Objetivo:
        * Evitar variables sueltas dentro de ConstruirFilaPorTipo y concentrar en un
        * solo objeto la información vigente del préstamo:
        * - préstamos encontrados del mismo tipo/subclave
        * - saldo total
        * - importe total
        * - préstamo principal más reciente
        * - liquidaCon
        * - fechas visibles
        * - bandera de vigencia real
        *
        * Importante:
        * Esta clase no representa una tabla de base de datos.
        * Solo es un contenedor interno para ordenar la rutina.
        * ============================================================================ */
        private sealed class DatosPrestamoNoPersonal
        {
            public List<PrestamoVigenteDto> PrestamosDelTipo { get; set; } = new();

            public decimal SaldoTotal { get; set; }

            public decimal ImporteTotal { get; set; }

            public PrestamoVigenteDto? PrestamoPrincipal { get; set; }

            public decimal LiquidaCon { get; set; }

            public DateTime? FechaPrestamo { get; set; }

            public DateTime? FechaVencimiento { get; set; }

            public bool EstaVigente { get; set; }
        }

        /* ============================================================================
        * OBTENER DATOS DEL PRÉSTAMO
        * ----------------------------------------------------------------------------
        * Primera etapa de la rutina.
        *
        * Responsabilidades:
        * 1. Filtrar los préstamos vigentes que corresponden al tipo/subclave actual.
        * 2. Sumar saldo e importe original del pagaré.
        * 3. Seleccionar el préstamo principal más reciente.
        * 4. Obtener datos visibles: fechas, liquidaCon y bandera de vigencia.
        *
        * Regla:
        * Un préstamo se considera vigente real cuando existe préstamo principal
        * y su SaldoPrestamo es mayor a cero.
        *
        * Nota:
        * Si el saldo es negativo, no se considera adeudo vigente, pero puede mostrarse
        * después como devolución pendiente.
        * ============================================================================ */
        private DatosPrestamoNoPersonal ObtenerDatosPrestamo(
            TipoPrestamoDto tipo,
            List<PrestamoVigenteDto> vigentes)
        {
            var prestamosDelTipo = vigentes
                .Where(p =>
                    p.TipoPrestamo == tipo.ClavePrestamo &&
                    (p.SubCve ?? 0) == (tipo.SubCve ?? 0))
                .ToList();

            var prestamoPrincipal = prestamosDelTipo
                .OrderByDescending(x => x.FechaPrestamo ?? DateTime.MinValue)
                .FirstOrDefault();

            return new DatosPrestamoNoPersonal
            {
                PrestamosDelTipo = prestamosDelTipo,
                SaldoTotal = prestamosDelTipo.Sum(x => x.SaldoPrestamo),
                ImporteTotal = prestamosDelTipo.Sum(x => x.ImportePagare),
                PrestamoPrincipal = prestamoPrincipal,
                LiquidaCon = prestamoPrincipal?.LiquidaCon ?? 0m,
                FechaPrestamo = prestamoPrincipal?.FechaPrestamo,
                FechaVencimiento = prestamoPrincipal?.FechaVencimiento,
                EstaVigente = prestamoPrincipal != null &&
                              prestamoPrincipal.SaldoPrestamo > 0
            };
        }

        /* ============================================================================
        * DTO INTERNO: ResultadoAlcanceNoPersonal
        * ----------------------------------------------------------------------------
        * Agrupa el resultado de la etapa de alcance/proyección.
        *
        * Contiene:
        * - si debe ejecutarse la sección de alcance
        * - cuánto puede solicitar el socio
        * - importe líquido calculado
        *
        * Objetivo:
        * Separar la decisión de proyectar del cálculo visual de la fila.
        * ============================================================================ */

        private sealed class ResultadoAlcanceNoPersonal
        {
            public bool RealizarRutinasSeccionAlcance { get; set; }

            public decimal PuedeSolicitar { get; set; }

            public decimal ImporteLiquido { get; set; }
        }

        /* ============================================================================
        * EVALUAR ALCANCE DEL PRÉSTAMO
        * ----------------------------------------------------------------------------
        * Segunda etapa de la rutina.
        *
        * Responsabilidades:
        * 1. Determinar si el tipo de préstamo debe ejecutar alcance/proyección.
        * 2. Validar renovación si existe saldo vigente y ClaveRenovacion = "1".
        * 3. Ejecutar CalcularAlcanceNoPersonal únicamente cuando aplica.
        *
        * Regla importante:
        * No decide si la fila se muestra.
        * Solo decide si se calculan:
        * - PuedeSolicitar
        * - ImporteLiquido
        * - bandera EsProyeccion
        *
        * Referencia VB:
        * Replica la bandera realizarRutinasSeccionAlcance.
        * ============================================================================ */
        private ResultadoAlcanceNoPersonal EvaluarAlcancePrestamo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            DatosPrestamoNoPersonal datosPrestamo)
        {
            bool realizarRutinasSeccionAlcance =
                DebeRealizarRutinasSeccionAlcance(
                    ctx,
                    tipo,
                    datosPrestamo.PrestamoPrincipal);

            if (realizarRutinasSeccionAlcance &&
                datosPrestamo.PrestamoPrincipal != null &&
                datosPrestamo.PrestamoPrincipal.SaldoPrestamo > 0 &&
                (tipo.ClaveRenovacion ?? "").Trim() == "1")
            {
                bool cumpleRenovacion = CumplePorcentajeRenovacion(
                    datosPrestamo.PrestamoPrincipal,
                    tipo);

                if (!cumpleRenovacion)
                    realizarRutinasSeccionAlcance = false;
            }

            decimal puedeSolicitar = 0m;
            decimal importeLiquido = 0m;

            if (realizarRutinasSeccionAlcance)
            {
                (puedeSolicitar, importeLiquido) = CalcularAlcanceNoPersonal(
                    ctx,
                    tipo,
                    datosPrestamo.SaldoTotal,
                    datosPrestamo.LiquidaCon,
                    tipo.PlazoMaximo);
            }

            return new ResultadoAlcanceNoPersonal
            {
                RealizarRutinasSeccionAlcance = realizarRutinasSeccionAlcance,
                PuedeSolicitar = puedeSolicitar,
                ImporteLiquido = importeLiquido
            };
        }


        /* ============================================================================
        * CALCULAR DESCUENTO DEL PRÉSTAMO VIGENTE
        * ----------------------------------------------------------------------------
        * Tercera etapa de la rutina.
        *
        * Responsabilidad:
        * Calcular el descuento/amortización visible cuando existe préstamo vigente.
        *
        * Equivalencia VB:
        * Representa el cálculo de AmortAnt:
        * - si saldo < amortización, toma saldo
        * - si no, toma importe amortización
        *
        * Si no hay préstamo vigente real, regresa 0.
        * ============================================================================ */
        private decimal CalcularDescuentoPrestamo(
            DatosPrestamoNoPersonal datosPrestamo)
        {
            if (!datosPrestamo.EstaVigente ||
                datosPrestamo.PrestamoPrincipal == null)
            {
                return 0m;
            }

            return _prestamoCalculatorService.CalcularDescuento(
                datosPrestamo.PrestamoPrincipal.SaldoPrestamo,
                datosPrestamo.PrestamoPrincipal.ImporteAmortizacion);
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
            decimal liquidaCon,
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
                    numeroPagos,
                    puedeSolicitar,
                    liquidaCon);
            }

            if (tipo.ClavePrestamo == "PR")
            {
                return CalcularAlcancePrendario(
                    ctx,
                    tipo,
                    numeroPagos,
                    liquidaCon);
            }

            if (tipo.ClavePrestamo == "RE")
            {
                return CalcularAlcanceRefaccionario(
                    ctx,
                    tipo,
                    numeroPagos,
                    liquidaCon);
            }

            if (tipo.ClavePrestamo == "PV")
            {
                return CalcularAlcanceViajes(
                    ctx,
                    tipo,
                    puedeSolicitar,
                    numeroPagos,
                    liquidaCon);
            }

            return CalcularAlcanceGeneralNoPersonal(
                ctx,
                tipo,
                puedeSolicitar,
                numeroPagos,
                liquidaCon);
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
        * VB: Caso especial EV / EsLiquido = "S"
        * ------------------------------------------------------------
        * Replica el bloque legacy:
        *
        * tbImporteLiquido = tbPuedeSolicitar - tbLiquidacon
        *
        * TMPintereses = CalcularInteresAPrestamo(tbPuedeSolicitar, ...)
        *
        * Si DiasAdic > 0:
        *   interés diario = (tbPuedeSolicitar * tasa anual) / 360
        *   TMPintereses += interés diario * DiasAdic
        *
        * Seguro y fondo se calculan sobre:
        *   tbPuedeSolicitar + TMPintereses
        *
        * Finalmente:
        *   tbPuedeSolicitar = tbPuedeSolicitar + intereses + seguro + fondo
        * ============================================================ */
        private (decimal puedeSolicitar, decimal importeLiquido) CalcularAlcanceEventosSociales(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            int numeroPagos,
            decimal importeLiquidoObjetivo,
            decimal liquidaCon)
        {
            if (importeLiquidoObjetivo <= 0 || numeroPagos <= 0)
                return (0m, 0m);

            importeLiquidoObjetivo = Math.Round(importeLiquidoObjetivo, 2);

            decimal importeLiquidoReal = importeLiquidoObjetivo - liquidaCon;

            if (importeLiquidoReal < 0)
                importeLiquidoReal = 0m;

            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            decimal intereses = CalcularInteresAPrestamo(
                importeLiquidoObjetivo,
                tasaPeriodo,
                numeroPagos);

            DateTime primerPago = ObtenerPrimerPago(ctx);

            int diasAdic = CalcularDiasAdicionales(
                ctx,
                tipo.ClavePrestamo,
                primerPago);

            intereses += CalcularInteresDiasAdicionales(
                tipo,
                importeLiquidoObjetivo,
                diasAdic);

            intereses = Math.Round(intereses, 2);

            decimal seguro = CalcularSeguroPasivo(
                importeLiquidoObjetivo,
                intereses,
                tipo);

            decimal fondo = CalcularFondoGarantia(
                importeLiquidoObjetivo,
                intereses,
                tipo);

            decimal puedeSolicitarFinal = Math.Round(
                importeLiquidoObjetivo +
                intereses +
                seguro +
                fondo,
                2);

            return (
                puedeSolicitarFinal,
                Math.Round(importeLiquidoReal, 2)
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

            var orden = new[]
            {
                "ES", "PC",
                "DN",
                "EV",
                "EX",
                "PP",
                "PR",
                "VA",
                "RE",
                "PV",
                "PE",
                "VI",
                "GM",
                "PH"
            };

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
                ("DN", _) => "DESASTRE NATURAL",
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

                "DN" => 20,
                "EV" => 30,
                "EX" => 40,

                // PERSONAL se agrega desde PrestamoPersonalService
                "PP" => 50,

                "PR" => 60,
                "VA" => 70,
                "RE" => 80,
                "PV" => 90,
                "PE" => 100,
                "VI" => 110,
                "GM" => 120,
                "PH" => 130,

                _ => 999
            };
        }
    }
}