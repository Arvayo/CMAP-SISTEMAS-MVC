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

            var resultado = new List<EstadoCuentaRowsDto>();

            foreach (var tipo in tiposPrestamo)
            {
                var fila = ConstruirFilaPorTipo(contexto, tipo, prestamosVigentes);
                resultado.Add(fila);
            }

            AgregarFilasProyectadas(contexto, resultado, prestamosVigentes);

            return resultado
                .OrderBy(x => x.OrdenVisual)
                .ThenBy(x => x.SubClave)
                .ToList();
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */
        private void AgregarFilasProyectadas(
        EstadoCuentaContextDto ctx,
        List<EstadoCuentaRowsDto> resultado,
        List<PrestamoVigenteDto> vigentes)
        {
            AgregarFilaSiNoExiste(resultado, CrearFilaProyectada(ctx, "ES", 0, "EVENTOS SOCIALES", 30, 30000m));

            AgregarFilaSiNoExiste(resultado, CrearFilaProyectada(ctx, "PR", 0, "PRENDARIO NORMAL", 15, 15000m));
            AgregarFilaSiNoExiste(resultado, CrearFilaProyectada(ctx, "PR", 1, "PRENDARIO TIPO A", 15, 20000m));
            AgregarFilaSiNoExiste(resultado, CrearFilaProyectada(ctx, "PR", 2, "PRENDARIO TIPO B", 24, 30000m));
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */
        private void AgregarFilaSiNoExiste(
        List<EstadoCuentaRowsDto> resultado,
        EstadoCuentaRowsDto nuevaFila)
        {
            bool existe = resultado.Any(x =>
                x.ClavePrestamo == nuevaFila.ClavePrestamo &&
                NormalizarSubClave(x.ClavePrestamo, x.SubClave) == NormalizarSubClave(nuevaFila.ClavePrestamo, nuevaFila.SubClave));

            if (!existe)
            {
                resultado.Add(nuevaFila);
            }
        }

        private int NormalizarSubClave(string clavePrestamo, int? subClave)
        {
            if (clavePrestamo == "PR")
                return subClave ?? 0;

            return subClave ?? 0;
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */
        private EstadoCuentaRowsDto CrearFilaProyectada(
        EstadoCuentaContextDto ctx,
        string clavePrestamo,
        int subClave,
        string nombrePrestamo,
            int plazoMeses,
        decimal importeLiquido)
        {
            decimal vecesAhorro = ObtenerVecesAhorroProyeccion(clavePrestamo, subClave);

            decimal topePorAhorros = _prestamoCalculatorService.CalcularTopePorAhorros(
                ctx.MisAhorros,
                vecesAhorro);

            decimal puedeSolicitar = topePorAhorros;

            if (puedeSolicitar < 0)
                puedeSolicitar = 0;

            if (importeLiquido > puedeSolicitar)
                importeLiquido = puedeSolicitar;

            decimal descuento = CalcularDescuentoProyeccion(
                clavePrestamo,
                subClave,
                importeLiquido,
                plazoMeses);

            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = clavePrestamo,
                SubClave = subClave,
                NombrePrestamo = nombrePrestamo,

                FechaPrestamo = null,
                ImportePrestamo = 0m,
                PlazoMeses = plazoMeses,
                FechaVencimiento = null,

                SaldoPrestamo = 0m,
                CantidadPuedeSolicitar = puedeSolicitar,

                ImporteLiquido = ObtenerImporteLiquidoBase(clavePrestamo, subClave),
                Descuento = descuento,
                LiquidaCon = 0m,

                EstaVigente = false,
                EsProyeccion = true,
                OrdenVisual = ObtenerOrdenVisual(clavePrestamo, subClave)
            };
        }

        private decimal ObtenerImporteLiquidoBase(string clavePrestamo, int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("PR", null) => 15000m,
                ("PR", 0) => 15000m,
                ("PR", 1) => 20000m,
                ("PR", 2) => 30000m,
                ("ES", _) => 30000m,
                _ => 0m
            };
        }

        private decimal CalcularImporteLiquidoFila(
        string clavePrestamo,
        int? subClave,
        bool estaVigente)
        {
            return (clavePrestamo, subClave, estaVigente) switch
            {
                ("PR", null, false) => 15000m,
                ("PR", 0, false) => 15000m,
                ("PR", 1, false) => 20000m,
                ("PR", 2, false) => 30000m,
                _ => 0m
            };
        }

        private decimal CalcularDescuentoProyeccion(
        string clavePrestamo,
        int subClave,
        decimal importeLiquido,
        int plazoMeses)
        {
            if (importeLiquido <= 0 || plazoMeses <= 0)
                return 0m;

            return (clavePrestamo, subClave) switch
            {
                ("ES", 0) => Math.Round(importeLiquido / plazoMeses, 2),
                ("PR", 0) => 0m,
                ("PR", 1) => 0m,
                ("PR", 2) => 0m,
                _ => 0m
            };
        }
        private decimal ObtenerVecesAhorroProyeccion(string clavePrestamo, int subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", 0) => 3.5m,
                ("PR", 0) => 3.5m,
                ("PR", 1) => 3.5m,
                ("PR", 2) => 3.5m,
                _ => 3.5m
            };
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

            var lista = await query.ToListAsync();

            var orden = new[] { "ES", "PC", "PP", "PR", "RE", "VI" };

            return lista
                .Where(x => orden.Contains(x.ClavePrestamo))
                .GroupBy(x => x.ClavePrestamo)
                .Select(g => g.First())
                .OrderBy(x => Array.IndexOf(orden, x.ClavePrestamo))
                .ToList();
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

            bool estaVigente = prestamoPrincipal != null;
            bool esProyeccion = !estaVigente;

            decimal importeLiquido = CalcularImporteLiquidoFila(
                tipo.ClavePrestamo,
                prestamoPrincipal?.SubCve,
                estaVigente);

            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = prestamoPrincipal?.SubCve ?? 0,
                NombrePrestamo = ObtenerNombreVisible(tipo.ClavePrestamo, prestamoPrincipal?.SubCve),

                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importeTotal,
                PlazoMeses = tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,

                SaldoPrestamo = saldoTotal,
                CantidadPuedeSolicitar = puedeSolicitar,

                ImporteLiquido = importeLiquido,
                Descuento = descuento,
                LiquidaCon = liquidaCon,

                EstaVigente = estaVigente,
                EsProyeccion = esProyeccion,
                OrdenVisual = ObtenerOrdenVisual(tipo.ClavePrestamo, prestamoPrincipal?.SubCve)
            };
        }

        private string ObtenerNombreVisible(string clavePrestamo, int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("PC", _) => "COMPLEMENTARIO",
                ("ES", _) => "EVENTOS SOCIALES",
                ("PP", _) => "PRÉSTAMO PERSONAL",

                // Muy importante:
                ("PR", 0) => "PRENDARIO NORMAL",
                ("PR", null) => "PRENDARIO NORMAL",
                ("PR", 1) => "PRENDARIO TIPO A",
                ("PR", 2) => "PRENDARIO TIPO B",

                ("RE", _) => "PRÉSTAMO REFACCIONAR",
                ("VA", _) => "VIAJES T.",
                ("VI", _) => "VIAJES T.",

                _ => clavePrestamo
            };
        }

        private int ObtenerOrdenVisual(string clavePrestamo, int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("PC", _) => 1,
                ("ES", _) => 2,
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