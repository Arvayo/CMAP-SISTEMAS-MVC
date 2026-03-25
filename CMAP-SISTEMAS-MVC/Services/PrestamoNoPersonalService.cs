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
    /// Encapsula la lógica de préstamos NO personales:
    ///  - obtener tipos de préstamo
    ///  - obtener préstamos vigentes
    ///  - construir filas del estado de cuenta
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
            var tiposPrestamo = await ObtenerTiposPrestamoAsync(contexto);

            var prestamosVigentes = await ObtenerPrestamosVigentesAsync(
                contexto.ClavePension,
                contexto.FechaSistema);

            var resultado = new List<EstadoCuentaRowsDto>(tiposPrestamo.Count);

            foreach (var tipo in tiposPrestamo)
            {
                var fila = ConstruirFilaPorTipo(contexto, tipo, prestamosVigentes);
                resultado.Add(fila);
            }

            return resultado;
        }

        /* ============================================================
         * SECCIÓN B: TIPOS DE PRÉSTAMO (TP + DP)
         * ============================================================ */
        private async Task<List<TipoPrestamoDto>> ObtenerTiposPrestamoAsync(EstadoCuentaContextDto ctx)
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

            return await query
                .OrderBy(x => x.ClavePrestamo)
                .ThenBy(x => x.PlazoMaximo)
                .ThenBy(x => x.TasaIntNormal)
                .ToListAsync();
        }

        /* ============================================================
         * SECCIÓN C: PRÉSTAMOS VIGENTES
         * ============================================================ */
        private async Task<List<PrestamoVigenteDto>> ObtenerPrestamosVigentesAsync(
            string clavePension,
            DateTime fechaSistema)
        {
            var rows = await ObtenerPrestamosVigentesSqlAsync(clavePension, fechaSistema);

            var resultado = new List<PrestamoVigenteDto>(rows.Count);

            foreach (var row in rows)
            {
                decimal liquidaCon = 0m;

                if (row.SaldoPrestamo != 0)
                {
                    liquidaCon = await _prestamoCalculatorService.ObtenerLiquidaConAsync(row.Id, fechaSistema);
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
                    ORDER BY tp.TipoPrestamo, tp.FechaPrestamo DESC
                ")
                .AsNoTracking()
                .ToListAsync();
        }

        /* ============================================================
         * SECCIÓN D: CONSTRUCCIÓN DE FILAS
         * ============================================================ */
        private EstadoCuentaRowsDto ConstruirFilaPorTipo(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            List<PrestamoVigenteDto> vigentes)
        {
            var prestamosDelTipo = vigentes
                .Where(p => p.TipoPrestamo == tipo.ClavePrestamo)
                .ToList();

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

            decimal topePorAhorros = _prestamoCalculatorService.CalcularTopePorAhorros(
                ctx.MisAhorros,
                tipo.VecesAhorro);

            decimal puedeSolicitar = topePorAhorros - saldoTotal;

            if (puedeSolicitar < 0)
                puedeSolicitar = 0;

            decimal descuento = 0m;

            if (prestamoPrincipal != null)
            {
                descuento = _prestamoCalculatorService.CalcularDescuento(
                    prestamoPrincipal.SaldoPrestamo,
                    prestamoPrincipal.ImporteAmortizacion);
            }

            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = tipo.SubCve,
                NombrePrestamo = tipo.NombrePrestamo,

                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importeTotal,
                PlazoMeses = tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,

                SaldoPrestamo = saldoTotal,
                CantidadPuedeSolicitar = puedeSolicitar,

                ImporteLiquido = 0,
                Descuento = descuento,
                LiquidaCon = liquidaCon
            };
        }
    }
}