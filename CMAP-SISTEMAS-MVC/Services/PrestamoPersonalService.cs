using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Services
{
    /// <summary>
    /// ============================================================
    /// SERVICIO: PrestamoPersonalService
    /// ------------------------------------------------------------
    /// Encapsula la lógica del préstamo personal (PP):
    ///  - obtener configuración PP
    ///  - obtener préstamo PP vigente/histórico relevante
    ///  - validar renovación
    ///  - construir fila(s) del estado de cuenta
    /// ============================================================
    /// </summary>
    public class PrestamoPersonalService : IPrestamoPersonalService
    {
        /* ============================================================
         * CAMPOS PRIVADOS
         * ============================================================ */
        private readonly Cmap54SistemasContext _context;
        private readonly IPrestamoCalculatorService _prestamoCalculatorService;

        /* ============================================================
         * CONSTRUCTOR
         * ============================================================ */
        public PrestamoPersonalService(
            Cmap54SistemasContext context,
            IPrestamoCalculatorService prestamoCalculatorService)
        {
            _context = context;
            _prestamoCalculatorService = prestamoCalculatorService;
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */
        public async Task<ResultadoPrestamoPersonalDto> GenerarPrestamosPersonalesAsync(
            EstadoCuentaContextDto contexto)
        {
            var tiposPP = await ObtenerTiposPrestamoPersonalAsync(contexto);
            var prestamoPP = await ObtenerPrestamoPersonalAsync(
                contexto.ClavePension,
                contexto.FechaSistema);

            var resultado = new ResultadoPrestamoPersonalDto();

            // Caso 1: hay préstamo personal vigente
            if (prestamoPP != null)
            {
                var resumen = ConstruirResumenPrestamoPersonal(contexto, tiposPP, prestamoPP);
                resultado.Resumen = resumen;

                // Solo si puede renovar mostramos proyección
                if (resumen.PuedeRenovar)
                {
                    foreach (var tipo in tiposPP)
                    {
                        var fila = await ConstruirFilaPrestamoPersonalAsync(
                            contexto,
                            tipo,
                            prestamoPP);

                        if (fila != null)
                        {
                            resultado.FilasProyeccion.Add(fila);
                        }
                    }
                }

                return resultado;
            }

            // Caso 2: no hay préstamo personal vigente -> proyección directa
            foreach (var tipo in tiposPP)
            {
                var fila = await ConstruirFilaPrestamoPersonalAsync(
                    contexto,
                    tipo,
                    null);

                if (fila != null)
                {
                    resultado.FilasProyeccion.Add(fila);
                }
            }

            resultado.Resumen = new PrestamoPersonalResumenDTO
            {
                TienePrestamoVigente = false,
                PuedeRenovar = true,
                CumplePago = true,
                CumplePlazo = true,
                DiasFaltantes = 0,
                MontoFaltanteParaRenovar = 0m,
                SaldoPrestamoActivo = 0m,
                LiquidaConPrestamoActivo = 0m,
                MensajeResultado = "Sin préstamo personal vigente. Se muestra proyección."
            };

            return resultado;
        }

        private PrestamoPersonalResumenDTO ConstruirResumenPrestamoPersonal(
        EstadoCuentaContextDto ctx,
        List<TipoPrestamoDto> tiposPP,
        PrestamoVigenteDto prestamoPP)
        {
            var tipoBase = tiposPP
                .OrderBy(x => x.SubCve)
                .FirstOrDefault();

            if (tipoBase == null)
            {
                return new PrestamoPersonalResumenDTO
                {
                    TienePrestamoVigente = true,
                    PuedeRenovar = false,
                    CumplePago = false,
                    CumplePlazo = false,
                    DiasFaltantes = 0,
                    MontoFaltanteParaRenovar = 0m,
                    SaldoPrestamoActivo = prestamoPP.SaldoPrestamo,
                    LiquidaConPrestamoActivo = prestamoPP.LiquidaCon,
                    MensajeResultado = "No se encontró configuración del préstamo personal."
                };
            }

            decimal porcentajePagado = 0m;
            bool cumplePago = false;

            if (prestamoPP.ImportePagare > 0)
            {
                porcentajePagado = 1m - (prestamoPP.SaldoPrestamo / prestamoPP.ImportePagare);
                decimal porcentajeMinimoPagado = tipoBase.PorcenRenova / 100m;
                cumplePago = porcentajePagado >= porcentajeMinimoPagado;
            }

            bool cumplePlazo = true;
            int diasFaltantes = 0;

            if (prestamoPP.FechaPrestamo.HasValue && prestamoPP.FechaVencimiento.HasValue)
            {
                var diasTotales = (prestamoPP.FechaVencimiento.Value.Date - prestamoPP.FechaPrestamo.Value.Date).TotalDays;
                var diasTranscurridos = (ctx.FechaSistema.Date - prestamoPP.FechaPrestamo.Value.Date).TotalDays;

                if (diasTotales > 0)
                {
                    var porcentajeTiempo = (decimal)(diasTranscurridos / diasTotales);
                    var porcentajeMinimoTiempo = 0.20m;

                    cumplePlazo = porcentajeTiempo >= porcentajeMinimoTiempo;

                    if (!cumplePlazo)
                    {
                        var diasMinimos = (int)Math.Ceiling(diasTotales * (double)porcentajeMinimoTiempo);
                        diasFaltantes = diasMinimos - (int)Math.Floor(diasTranscurridos);

                        if (diasFaltantes < 0)
                            diasFaltantes = 0;
                    }
                }
            }

            decimal montoFaltante = 0m;

            if (prestamoPP.ImportePagare > 0)
            {
                decimal porcentajeMinimoPagado = tipoBase.PorcenRenova / 100m;
                decimal saldoMaximoPermitido = prestamoPP.ImportePagare * (1 - porcentajeMinimoPagado);

                if (prestamoPP.SaldoPrestamo > saldoMaximoPermitido)
                {
                    montoFaltante = prestamoPP.SaldoPrestamo - saldoMaximoPermitido;
                }
            }

            bool puedeRenovar = cumplePago && cumplePlazo;

            string mensaje;

            if (puedeRenovar)
            {
                mensaje = "Puede renovar préstamo personal. Se muestra proyección.";
            }
            else if (!cumplePago && !cumplePlazo)
            {
                mensaje = $"No puede renovar préstamo personal. Falta abonar {montoFaltante:N2} y faltan {diasFaltantes} días para cumplir plazo.";
            }
            else if (!cumplePago)
            {
                mensaje = $"No puede renovar préstamo personal. Falta abonar {montoFaltante:N2}.";
            }
            else
            {
                mensaje = $"No puede renovar préstamo personal. Restan {diasFaltantes} días para cumplir plazo.";
            }

            return new PrestamoPersonalResumenDTO
            {
                TienePrestamoVigente = true,
                PuedeRenovar = puedeRenovar,
                CumplePago = cumplePago,
                CumplePlazo = cumplePlazo,
                DiasFaltantes = diasFaltantes,
                MontoFaltanteParaRenovar = Math.Round(montoFaltante, 2),
                SaldoPrestamoActivo = prestamoPP.SaldoPrestamo,
                LiquidaConPrestamoActivo = prestamoPP.LiquidaCon,
                MensajeResultado = mensaje
            };
        }

        /* ============================================================
         * SECCIÓN A: CONFIGURACIÓN DE PP
         * ============================================================ */
        private async Task<List<TipoPrestamoDto>> ObtenerTiposPrestamoPersonalAsync(
            EstadoCuentaContextDto ctx)
        {
            return await (
                from tp in _context.TABLA_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                join dp in _context.DETALLE_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                    on tp.ClavePrestamo equals dp.ClavePrestamo
                where dp.TipoSocio == ctx.Estatus
                      && dp.Vigencia == ctx.Vigencia
                      && tp.ClavePrestamo == "PP"
                      && dp.Vigente == "S"
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
                })
                .OrderBy(x => x.SubCve)
                .ThenBy(x => x.PlazoMaximo)
                .ToListAsync();
        }

        /* ============================================================
         * SECCIÓN B: PRÉSTAMO PP EXISTENTE
         * ============================================================ */
        private async Task<PrestamoVigenteDto?> ObtenerPrestamoPersonalAsync(
        string clavePension,
        DateTime fechaSistema)
        {
            var row = await _context.TABLA_DE_PRESTAMOS
                .AsNoTracking()
                .Where(p => p.ClavePension == clavePension
                         && p.TipoPrestamo == "PP"
                         && p.EstatusPrestamo == "VI")
                .OrderByDescending(p => p.FechaPrestamo)
                .ThenByDescending(p => p.FechaUltimoPago)
                .Select(p => new PrestamoVigenteDto
                {
                    Id = p.Id ?? 0,
                    TipoPrestamo = p.TipoPrestamo ?? string.Empty,
                    SubCve = p.SubCve,
                    SaldoPrestamo = p.SaldoPrestamo ?? 0,
                    ImportePagare = p.ImportePagare ?? 0,
                    ImporteAmortizacion = p.ImporteAmortizacion ?? 0,
                    NumMesesPrestamo = p.NumMesesPrestamo ?? 0,
                    NumeroPagare = p.NumeroPagare ?? 0,
                    FechaPrestamo = p.FechaPrestamo,
                    FechaVencimiento = p.FechaVencimiento
                })
                .FirstOrDefaultAsync();

            if (row == null)
                return null;

            row.LiquidaCon = row.SaldoPrestamo != 0
                ? await _prestamoCalculatorService.ObtenerLiquidaConAsync(row.Id, fechaSistema)
                : 0m;

            return row;
        }
        /* ============================================================
         * SECCIÓN C: CONSTRUCCIÓN DE FILA PP
         * ============================================================ */
        private async Task<EstadoCuentaRowsDto?> ConstruirFilaPrestamoPersonalAsync(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            PrestamoVigenteDto? prestamoPP)
        {
            decimal saldoPrestamo = prestamoPP?.SaldoPrestamo ?? 0m;
            decimal importePrestamo = prestamoPP?.ImportePagare ?? 0m;
            DateTime? fechaPrestamo = prestamoPP?.FechaPrestamo;
            DateTime? fechaVencimiento = prestamoPP?.FechaVencimiento;
            decimal liquidaCon = prestamoPP?.LiquidaCon ?? 0m;

            decimal puedeSolicitar = 0m;
            decimal importeLiquido = 0m;
            decimal descuento = 0m;

            bool tienePrestamoVigente = prestamoPP != null && saldoPrestamo > 0;
            bool puedeRenovar = false;

            if (!tienePrestamoVigente)
            {
                puedeRenovar = true;
            }
            else
            {
                puedeRenovar = PuedeRenovarPrestamoPersonal(ctx, tipo, prestamoPP!);
            }

            if (puedeRenovar)
            {
                puedeSolicitar = CalcularAlcancePrestamoPersonal(ctx, tipo, saldoPrestamo);

                if (puedeSolicitar < 0)
                    puedeSolicitar = 0;

                descuento = CalcularDescuentoPrestamoPersonal(ctx, tipo, puedeSolicitar);

                importeLiquido = await CalcularImporteLiquidoPrestamoPersonalAsync(
                    ctx,
                    tipo,
                    prestamoPP,
                    puedeSolicitar);
            }

            if (saldoPrestamo <= 0 && puedeSolicitar <= 0)
                return null;

            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = tipo.SubCve,
                NombrePrestamo = tipo.NombrePrestamo,

                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importePrestamo,
                PlazoMeses = prestamoPP?.NumMesesPrestamo ?? tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,

                SaldoPrestamo = saldoPrestamo,
                CantidadPuedeSolicitar = puedeSolicitar,
                ImporteLiquido = importeLiquido,
                Descuento = descuento,
                LiquidaCon = liquidaCon
            };
        }

        /* ============================================================
         * SECCIÓN D: REGLAS PP
         * ============================================================ */
        private bool PuedeRenovarPrestamoPersonal(
     EstadoCuentaContextDto ctx,
     TipoPrestamoDto tipo,
     PrestamoVigenteDto prestamoPP)
        {
            if (prestamoPP.ImportePagare <= 0)
                return false;

            decimal porcentajePagado =
                1m - (prestamoPP.SaldoPrestamo / prestamoPP.ImportePagare);

            decimal porcentajeMinimoPagado = tipo.PorcenRenova / 100m;

            bool cumplePago = porcentajePagado >= porcentajeMinimoPagado;

            bool cumpleTiempo = true;

            if (prestamoPP.FechaPrestamo.HasValue && prestamoPP.FechaVencimiento.HasValue)
            {
                var diasTotales = (prestamoPP.FechaVencimiento.Value - prestamoPP.FechaPrestamo.Value).TotalDays;
                var diasTranscurridos = (ctx.FechaSistema - prestamoPP.FechaPrestamo.Value).TotalDays;

                if (diasTotales > 0)
                {
                    decimal porcentajeTiempo = (decimal)(diasTranscurridos / diasTotales);

                    // 🔥 regla real (20%)
                    decimal porcentajeMinimoTiempo = 0.20m;

                    cumpleTiempo = porcentajeTiempo >= porcentajeMinimoTiempo;
                }
            }

            return cumplePago && cumpleTiempo;
        }

        private decimal CalcularAlcancePrestamoPersonal(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal saldoPrestamoAnterior)
        {
            int numeroPagos = ctx.Estatus == "A"
                ? tipo.PlazoMaximo * 2
                : tipo.PlazoMaximo;

            decimal amortizacionAnterior = 0m;

            decimal alcanceSueldo =
                (ctx.TotSueldo - ctx.ElLimite + amortizacionAnterior) * numeroPagos;

            decimal factorPP = ObtenerFactorPrestamoPersonal(ctx);
            decimal alcanceAhorrosPP = ctx.MisAhorros * factorPP;

            decimal puedeSolicitar = Math.Min(alcanceSueldo, alcanceAhorrosPP);

            if (puedeSolicitar < 0)
                puedeSolicitar = 0;

            decimal topeGlobal = ctx.MisAhorros * factorPP;

            if ((puedeSolicitar + ctx.SaldoP - ctx.SdoPrestamoPP) > topeGlobal && topeGlobal > 0)
            {
                puedeSolicitar -= ((puedeSolicitar + ctx.SaldoP - ctx.SdoPrestamoPP) - topeGlobal);
            }

            if (puedeSolicitar < 0)
                puedeSolicitar = 0;

            return puedeSolicitar;
        }

        private decimal CalcularDescuentoPrestamoPersonal(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal puedeSolicitar)
        {
            int numeroPagos = ctx.Estatus == "A"
                ? tipo.PlazoMaximo * 2
                : tipo.PlazoMaximo;

            if (numeroPagos <= 0)
                return 0m;

            return Math.Round(puedeSolicitar / numeroPagos, 2);
        }

        private async Task<decimal> CalcularImporteLiquidoPrestamoPersonalAsync(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            PrestamoVigenteDto? prestamoPP,
            decimal puedeSolicitar)
        {
            if (puedeSolicitar <= 0)
                return 0m;

            decimal saldoAnterior = prestamoPP?.SaldoPrestamo ?? 0m;
            decimal liquidaConAnterior = prestamoPP?.LiquidaCon ?? 0m;

            decimal seguroPasivo = Math.Round(
                puedeSolicitar * (tipo.PorcenSeguroPasivo / 100m), 2);

            decimal fondoGarantia = Math.Round(
                puedeSolicitar * (tipo.PorcenFondoGarantia / 100m), 2);

            decimal importeLiquido = puedeSolicitar - seguroPasivo - fondoGarantia;

            if (saldoAnterior > 0)
            {
                importeLiquido -= liquidaConAnterior;
            }

            if (importeLiquido < 0)
                importeLiquido = 0m;

            await Task.CompletedTask;
            return importeLiquido;
        }

        private decimal ObtenerFactorPrestamoPersonal(EstadoCuentaContextDto ctx)
        {
            if (ctx.FechaIngreso == null)
                return 1.85m;

            DateTime fechaCorte = new DateTime(2011, 4, 13);

            return ctx.FechaIngreso <= fechaCorte
                ? 2.41m
                : 1.85m;
        }
    }
}