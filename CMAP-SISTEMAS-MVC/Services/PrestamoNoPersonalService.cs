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
                .ThenBy(x => x.SubClave)
                .ToList();
        }

        /* ============================================================
         * SECCIÓN 1: CONSTRUCCIÓN DE FILAS DEL ESTADO DE CUENTA
         * ============================================================ */

        private EstadoCuentaRowsDto? ConstruirFilaPorTipo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            List<PrestamoVigenteDto> vigentes)
        {
            List<PrestamoVigenteDto> prestamosDelTipo;

            if (tipo.ClavePrestamo == "PP")
            {
                prestamosDelTipo = vigentes
                    .Where(p => p.TipoPrestamo == "PP")
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

            if (tipo.ClavePrestamo == "PP" && prestamosDelTipo.Any())
            {
                prestamosDelTipo = prestamosDelTipo
                    .OrderByDescending(x => x.FechaPrestamo ?? DateTime.MinValue)
                    .Take(1)
                    .ToList();
            }

            decimal saldoTotal = prestamosDelTipo.Sum(x => x.SaldoPrestamo);
            decimal importeTotal = prestamosDelTipo.Sum(x => x.ImportePagare);

            var prestamoPrincipal = prestamosDelTipo
                .OrderByDescending(x => x.FechaPrestamo ?? DateTime.MinValue)
                .FirstOrDefault();

            decimal liquidaCon = prestamoPrincipal?.LiquidaCon ?? 0m;
            DateTime? fechaPrestamo = prestamoPrincipal?.FechaPrestamo;
            DateTime? fechaVencimiento = prestamoPrincipal?.FechaVencimiento;

            bool estaVigente = prestamoPrincipal != null && prestamoPrincipal.SaldoPrestamo > 0;
            bool realizarProyeccion = !estaVigente && tipo.Vigente == "S";
            bool esProyeccion = realizarProyeccion;

            var soloMostrarSiVigente = new[] { "GM", "EX", "PH" };

            if (soloMostrarSiVigente.Contains(tipo.ClavePrestamo) && !estaVigente)
                return null;

            if (!estaVigente && !realizarProyeccion)
                return null;

            decimal descuento = 0m;

            if (estaVigente && prestamoPrincipal != null)
            {
                descuento = _prestamoCalculatorService.CalcularDescuento(
                    prestamoPrincipal.SaldoPrestamo,
                    prestamoPrincipal.ImporteAmortizacion);
            }

            decimal puedeSolicitar = 0m;
            decimal importeLiquido = 0m;

            if (realizarProyeccion)
            {
                (puedeSolicitar, importeLiquido) = CalcularAlcanceNoPersonal(
                    ctx,
                    tipo,
                    saldoTotal,
                    tipo.PlazoMaximo);
            }

            if (estaVigente)
                importeLiquido = 0m;

            int subClave = prestamoPrincipal?.SubCve ?? tipo.SubCve ?? 0;

            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = subClave,
                NombrePrestamo = ObtenerNombreVisible(tipo.ClavePrestamo, subClave),

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
                EsProyeccion = esProyeccion,
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

            return CalcularAlcanceGeneralNoPersonal(
                ctx,
                tipo,
                puedeSolicitar,
                numeroPagos,
                saldoActualDelTipo);
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
            decimal importeLiquidoEv = CalcularImporteLiquidoEventosSociales(
                puedeSolicitarInicial,
                saldoActualDelTipo);

            decimal tasaPeriodo = ObtenerTasaPeriodo(ctx, tipo);

            decimal baseCalculo = alcancePorSueldo;

            decimal interesesEv = CalcularInteresAPrestamo(
                baseCalculo,
                tasaPeriodo,
                numeroPagos);

            decimal seguroEv = CalcularSeguroPasivo(
                baseCalculo,
                interesesEv,
                tipo);

            decimal fondoEv = CalcularFondoGarantia(
                baseCalculo,
                interesesEv,
                tipo);

            decimal puedeSolicitarEv = Math.Round(
                baseCalculo + interesesEv + seguroEv + fondoEv,
                2);

            return (
                puedeSolicitarEv,
                importeLiquidoEv
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
                    NombrePrestamo = tp.NombrePrestamo ?? string.Empty,

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

            var orden = new[] { "ES", "PC", "EV", "PR", "RE", "VI", "VA", "GM", "EX", "PH" };

            return lista
                .Where(x =>
                    orden.Contains(x.ClavePrestamo) &&
                    x.ClavePrestamo != "PP")
                .OrderBy(x => Array.IndexOf(orden, x.ClavePrestamo))
                .ThenBy(x => x.SubCve)
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

                if (row.SaldoPrestamo != 0)
                {
                    liquidaCon = await _prestamoCalculatorService.ObtenerLiquidaConAsync(
                        row.Id,
                        fechaSistema);
                }

                resultado.Add(new PrestamoVigenteDto
                {
                    Id = row.Id,
                    TipoPrestamo = row.TipoPrestamo,
                    SubCve = row.SubCve,
                    SaldoPrestamo = row.SaldoPrestamo,
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
                    AND tp.EstatusPrestamo = 'VI'
                    AND ISNULL(tp.TipoPrestamo, '') <> 'PP'
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
            int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", _) => "ESPECIAL",
                ("PC", _) => "COMPLEMENTARIO",
                ("EV", _) => "EVENTOS SOCIALES",

                ("PP", _) => "PERSONAL",

                ("PR", null) => "PRENDARIO NORMAL",
                ("PR", 0) => "PRENDARIO NORMAL",
                ("PR", 1) => "PRENDARIO TIPO A",
                ("PR", 2) => "PRENDARIO TIPO B",

                ("RE", _) => "REFACCIONARIO",
                ("VI", _) => "VIAJES T.",

                _ => clavePrestamo
            };
        }

        private int ObtenerOrdenVisual(
            string clavePrestamo,
            int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", _) => 1,
                ("PC", _) => 1,

                ("EV", _) => 2,

                ("PP", _) => 3,

                ("PR", null) => 4,
                ("PR", 0) => 4,
                ("PR", 1) => 5,
                ("PR", 2) => 6,

                ("RE", _) => 7,
                ("VA", _) => 8,
                ("VI", _) => 8,

                _ => 99
            };
        }
    }
}