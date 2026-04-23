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
    ///  - generar proyecciones temporales
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

                if (fila != null)
                resultado.Add(fila);
            }

            AgregarFilasProyectadas(contexto, resultado, prestamosVigentes);

            return resultado
                .OrderBy(x => x.OrdenVisual)
                .ThenBy(x => x.SubClave)
                .ToList();
        }

        /* ============================================================
         * SECCIÓN A: CONSTRUCCIÓN DE FILAS
         * ============================================================ */
        private EstadoCuentaRowsDto? ConstruirFilaPorTipo(
        EstadoCuentaContextDto ctx,
        TipoPrestamoDto tipo,
        List<PrestamoVigenteDto> vigentes)
        {
            var prestamosDelTipo = vigentes
                .Where(p =>
                    p.TipoPrestamo == tipo.ClavePrestamo &&
                    (p.SubCve ?? 0) == (tipo.SubCve ?? 0))
                .ToList();

            // Para PP, si por alguna razón existen varios vigentes, tomamos el más reciente.
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

            bool estaVigente = prestamoPrincipal != null;
            bool realizarProyeccion = !estaVigente && tipo.Vigente == "S";
            bool esProyeccion = realizarProyeccion;

            if (!estaVigente && !realizarProyeccion)
            {
                return null;
            }

            decimal descuento = 0m;

            if (prestamoPrincipal != null)
            {
                descuento = _prestamoCalculatorService.CalcularDescuento(
                    prestamoPrincipal.SaldoPrestamo,
                    prestamoPrincipal.ImporteAmortizacion);
            }

            var (puedeSolicitar, importeLiquido) = CalcularAlcanceNoPersonal(
                ctx,
                tipo,
                saldoTotal,
                tipo.PlazoMaximo);

            // Si existe préstamo vigente, no proyectamos líquido nuevo
            if (estaVigente)
            {
                importeLiquido = 0m;
            }

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
                PlazoMeses = tipo.PlazoMaximo,
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
         * SECCIÓN B: PROYECCIONES
         * ============================================================ */
        private void AgregarFilasProyectadas(
            EstadoCuentaContextDto ctx,
            List<EstadoCuentaRowsDto> resultado,
            List<PrestamoVigenteDto> vigentes)
        {
            // 1) Especial = ES
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "ES", 0, "ESPECIAL", 3, 8500.00m, 0m, 0m, 0m));

            // 2) Eventos Sociales = EV
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "EV", 0, "EVENTOS SOCIALES", 30, 35444.23m, 0m, 0m, 0m));

            // 3) Personal = PP (solo como fila general en esta sección)
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "PP", 0, "PRÉSTAMO PERSONAL", 15, 0m, 0m, 0m, 0m));

            // 4) Prendarios
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "PR", 0, "PRENDARIO NORMAL", 15, 16501.23m, 0m, 0m, 0m));

            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "PR", 1, "PRENDARIO TIPO A", 15, 22001.64m, 0m, 0m, 0m));

            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "PR", 2, "PRENDARIO TIPO B", 24, 34454.03m, 0m, 0m, 0m));

            // 5) Refaccionario
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "RE", 0, "PRÉSTAMO REFACCIONAR", 15, 11000.82m, 0m, 0m, 0m));

            // 6) Viajes
            AgregarFilaSiNoExiste(resultado,
                CrearFilaProyectada(ctx, "VI", 0, "VIAJES T.", 15, 11000.82m, 0m, 0m, 0m));
        }

        private EstadoCuentaRowsDto CrearFilaProyectada(
            EstadoCuentaContextDto ctx,
            string clavePrestamo,
            int subClave,
            string nombrePrestamo,
            int plazoMeses,
            decimal montoMaximo,
            decimal tasa,
            decimal seguro,
            decimal fondo)
        {
            var tipoTemp = new TipoPrestamoDto
            {
                ClavePrestamo = clavePrestamo,
                SubCve = subClave,
                NombrePrestamo = nombrePrestamo,
                PlazoMaximo = plazoMeses,
                MontoMaximo = montoMaximo,
                TasaIntNormal = tasa,
                PorcenSeguroPasivo = seguro,
                PorcenFondoGarantia = fondo,
                VecesAhorro = 3.5m,
                Vigente = "S",
                MesesMinCotizados = 6
            };

            var (puedeSolicitar, importeLiquido) = CalcularAlcanceNoPersonal(
                ctx,
                tipoTemp,
                0m,
                plazoMeses);

            decimal descuento = 0m;

            if (plazoMeses > 0)
            {
                descuento = Math.Round(puedeSolicitar / plazoMeses, 2);
            }

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

                ImporteLiquido = importeLiquido,
                Descuento = descuento,
                LiquidaCon = 0m,

                EstaVigente = false,
                EsProyeccion = true,
                OrdenVisual = ObtenerOrdenVisual(clavePrestamo, subClave)
            };
        }

        private void AgregarFilaSiNoExiste(
            List<EstadoCuentaRowsDto> resultado,
            EstadoCuentaRowsDto nuevaFila)
        {
            bool existe = resultado.Any(x =>
                x.ClavePrestamo == nuevaFila.ClavePrestamo &&
                NormalizarSubClave(x.ClavePrestamo, x.SubClave) ==
                NormalizarSubClave(nuevaFila.ClavePrestamo, nuevaFila.SubClave));

            if (!existe)
            {
                resultado.Add(nuevaFila);
            }
        }

        private int NormalizarSubClave(string clavePrestamo, int? subClave)
        {
            return subClave ?? 0;
        }

        /* ============================================================
         * SECCIÓN C: CÁLCULO DE ALCANCE
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

            decimal topeGlobalDisponible;

            if (ctx.EsSolicitudEspecial)
            {
                topeGlobalDisponible = decimal.MaxValue;
            }
            else
            {
                decimal topeGlobal = _prestamoCalculatorService
                    .CalcularTopePorAhorros(ctx.MisAhorros, 3.5m);

                topeGlobalDisponible = topeGlobal - ctx.SaldoPrestamosTopadosAhorro;

                // Evita castigar doble si el tipo ya tiene saldo vigente
                topeGlobalDisponible += saldoActualDelTipo;

                if (topeGlobalDisponible < 0)
                    topeGlobalDisponible = 0m;
            }

            decimal vecesAhorro = tipo.VecesAhorro > 0 ? tipo.VecesAhorro : 3.5m;

            decimal topeProducto = _prestamoCalculatorService.CalcularTopePorAhorros(
                ctx.MisAhorros,
                vecesAhorro);

            if (tipo.MontoMaximo > 0)
                topeProducto = Math.Min(topeProducto, tipo.MontoMaximo);

            if (tipo.FactorSobreAhorro > 0)
            {
                decimal topeFactor = ctx.MisAhorros * tipo.FactorSobreAhorro;
                topeProducto = Math.Min(topeProducto, topeFactor);
            }

            decimal puedeSolicitar = Math.Min(topeGlobalDisponible, topeProducto);

            if (puedeSolicitar < 0)
                puedeSolicitar = 0m;

            decimal importeLiquido = CalcularImporteLiquidoEstimado(
                puedeSolicitar,
                tipo,
                plazoMeses);

            return (
                Math.Round(puedeSolicitar, 2),
                Math.Round(importeLiquido, 2)
            );
        }

        private decimal CalcularImporteLiquidoEstimado(
            decimal puedeSolicitar,
            TipoPrestamoDto tipo,
            int plazoMeses)
        {
            if (puedeSolicitar <= 0)
                return 0m;

            if (tipo.EsLiquido == "S")
                return Math.Round(puedeSolicitar, 2);

            decimal tasa = tipo.TasaIntNormal;
            decimal seguro = tipo.PorcenSeguroPasivo;
            decimal fondo = tipo.PorcenFondoGarantia;

            decimal cargoInteres = 0m;
            decimal cargoSeguro = 0m;
            decimal cargoFondo = 0m;

            if (tasa > 0)
                cargoInteres = puedeSolicitar * (tasa / 100m);

            if (seguro > 0)
                cargoSeguro = puedeSolicitar * (seguro / 100m);

            if (fondo > 0)
                cargoFondo = puedeSolicitar * (fondo / 100m);

            decimal importeLiquido = puedeSolicitar - cargoInteres - cargoSeguro - cargoFondo;

            if (importeLiquido < 0)
                importeLiquido = 0m;

            return Math.Round(importeLiquido, 2);
        }

        /* ============================================================
         * SECCIÓN D: TIPOS DE PRÉSTAMO
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

            var es = lista.FirstOrDefault(x => x.ClavePrestamo == "ES");
            var pc = lista.FirstOrDefault(x => x.ClavePrestamo == "PC");

            var prestamoESoPC = es ?? pc;

            //  Eliminar ES y PC de la lista
            lista = lista
                .Where(x => x.ClavePrestamo != "ES" && x.ClavePrestamo != "PC")
                .ToList();

            //  Agregar solo uno (prioridad ES sobre PC)
            if (prestamoESoPC != null)
            {
                lista.Add(prestamoESoPC);
            }

            // Orden de visualización de NO PERSONALES
            // PP se excluye aquí porque se procesa aparte en el flujo de personales
            var orden = new[] { "ES", "EV", "PP", "PR", "RE", "VI", "PC" };

            return lista
               .Where(x =>
                    orden.Contains(x.ClavePrestamo) &&
                    x.ClavePrestamo != "PP") // PP se procesa aparte en el flujo de personales
               .OrderBy(x => Array.IndexOf(orden, x.ClavePrestamo))
               .ThenBy(x => x.SubCve)
               .ToList();
        }

        /* ============================================================
         * SECCIÓN E: PRÉSTAMOS VIGENTES
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
                    ORDER BY tp.TipoPrestamo, tp.FechaPrestamo DESC
                ")
                .AsNoTracking()
                .ToListAsync();
        }

        /* ============================================================
         * SECCIÓN F: PRESENTACIÓN
         * ============================================================ */
        private string ObtenerNombreVisible(string clavePrestamo, int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", _) => "ESPECIAL",
                ("EV", _) => "EVENTOS SOCIALES",
                ("PP", _) => "PRÉSTAMO PERSONAL",
                ("PC", _) => "COMPLEMENTARIO",

                ("PR", null) => "PRENDARIO NORMAL",
                ("PR", 0) => "PRENDARIO NORMAL",
                ("PR", 1) => "PRENDARIO TIPO A",
                ("PR", 2) => "PRENDARIO TIPO B",

                ("RE", _) => "PRÉSTAMO REFACCIONAR",
                ("VA", _) => "VARIOS T.",
                ("VI", _) => "VIAJES T.",

                _ => clavePrestamo
            };
        }

        private int ObtenerOrdenVisual(string clavePrestamo, int? subClave)
        {
            return (clavePrestamo, subClave) switch
            {
                ("ES", _) => 1,
                ("EV", _) => 2,
                ("PP", _) => 3,

                ("PR", null) => 4,
                ("PR", 0) => 4,
                ("PR", 1) => 5,
                ("PR", 2) => 6,

                ("RE", _) => 7,
                ("VA", _) => 8,
                ("VI", _) => 8,
                ("PC", _) => 9,
                _ => 99
            };
        }
    }
}