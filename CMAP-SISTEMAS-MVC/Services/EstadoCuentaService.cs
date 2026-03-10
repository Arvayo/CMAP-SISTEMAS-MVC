using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Services

{
    /// <summary>
    /// Servicio encargado de generar el estado de cuenta
    /// de préstamos para un socio.
    /// 
    /// Este servicio reemplaza la lógica del sistema VB
    /// contenida en:
    /// 
    /// ActualizaTablaTemporalReporte()
    /// NuevaAgregaPersonales()
    /// </summary>
    public class EstadoCuentaService
    {
        private readonly Cmap54SistemasContext _context;

        /// <summary>
        /// Constructor que recibe el DbContext de la base de datos.
        /// </summary>
        public EstadoCuentaService(Cmap54SistemasContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtiene los datos base del socio desde TABLA_DE_SOCIOS.
        /// </summary>
        private async Task<SocioBaseDto?> ObtenerSocioBaseAsync(string clavePension)
        {
            return await _context.TABLA_DE_SOCIOS
                .AsNoTracking()
                .Where(s => s.ClavePension == clavePension)
                .Select(s => new SocioBaseDto
                {
                    ClavePension = s.ClavePension ?? string.Empty,
                    SaldoAhorros = s.SaldoAhorros ?? 0,
                    SueldoNetoTotal = s.SueldoNetoTotal ?? 0,
                    SaldoPrestamos = s.SaldoPrestamos ?? 0,
                    Vigencia = s.VIGENCIA,
                    Situacion = s.SITUACION
                })
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Genera el estado de cuenta completo del socio.
        /// </summary>

        public async Task<List<EstadoCuentaRowsDto>> GenerarEstadoCuentaAsync(EstadoCuentaContextDto contexto)
        {
            var resultado = new List<EstadoCuentaRowsDto>();

            // 1. Leer datos base del socio
            var socio = await ObtenerSocioBaseAsync(contexto.ClavePension);

            if (socio != null)
            {
                contexto.MisAhorros = socio.SaldoAhorros;
                contexto.TotSueldo = socio.SueldoNetoTotal;
                contexto.SaldoP = socio.SaldoPrestamos;

                if (!string.IsNullOrWhiteSpace(socio.Vigencia))
                    contexto.Vigencia = socio.Vigencia;
            }

            // 2. Obtener tipos de préstamo
            var tiposPrestamo = await ObtenerTiposPrestamoAsync(contexto);

            // 3. Obtener préstamos vigentes
            var prestamosVigentes = await ObtenerPrestamosVigentesAsync(contexto.ClavePension);

            // 4. Procesar cada tipo
            foreach (var tipo in tiposPrestamo)
            {
                var fila = ProcesarTipoPrestamo(contexto, tipo, prestamosVigentes);

                if (fila != null)
                    resultado.Add(fila);
            }

            return resultado;
        }

        private async Task<List<PrestamoVigenteDto>> ObtenerPrestamosVigentesAsync(string clavePension)
        {
            return await _context.TABLA_DE_PRESTAMOS
                .AsNoTracking()
                .Where(p => p.ClavePension == clavePension && p.EstatusPrestamo == "VI")
                .Select(p => new PrestamoVigenteDto
                {
                    TipoPrestamo = p.TipoPrestamo,
                    SaldoPrestamo = p.SaldoPrestamo ?? 0,
                    FechaPrestamo = p.FechaPrestamo,
                    FechaVencimiento = p.FechaVencimiento,
                    ImportePagare = p.ImportePagare ?? 0
                })
                .Take(50)
                .ToListAsync();
        }
        /// <summary>
        /// Obtiene los tipos de préstamo configurados
        /// para el tipo de socio.
        /// </summary>
        private async Task<List<TipoPrestamoDto>> ObtenerTiposPrestamoAsync(EstadoCuentaContextDto contexto)
        {
            var query = _context.TABLA_DE_TIPOS_DE_PRESTAMOS
                .AsNoTracking()
                .Select(tp => new TipoPrestamoDto
                {
                    ClavePrestamo = tp.ClavePrestamo ?? string.Empty,
                    NombrePrestamo = tp.NombrePrestamo ?? string.Empty,
                    VecesAhorro = tp.VecesAhorro ?? 0,
                    PorcenRenova = tp.PorcenRenova ?? 0,
                    EsLiquido = tp.Esliquido,
                    ClaveRenovacion = tp.ClaveRenovacion,
                    PlazoRenovar = tp.PlazoRenovar ?? 0,
                    SubClave = null,
                    Vigente = "S",
                    PlazoMaximo = 0,
                    TasaIntNormal = 0,
                    MontoMaximo = 0,
                    PorcenSeguroPasivo = 0,
                    PorcenFondoGarantia = 0,
                    FactorSobreAhorro = 0,
                    MesesMinCotizados = 0
                })
                .Where(tp => !string.IsNullOrWhiteSpace(tp.ClavePrestamo))
                .OrderBy(tp => tp.ClavePrestamo);

            return await query.Take(20).ToListAsync();
        }


        /// <summary>
        /// Procesa un tipo de préstamo específico
        /// y calcula la información que se mostrará
        /// en el estado de cuenta.
        /// </summary>
        private EstadoCuentaRowsDto? ProcesarTipoPrestamo(
        EstadoCuentaContextDto contexto,
        TipoPrestamoDto tipo,
        List<PrestamoVigenteDto> prestamosVigentes)
        {
            var prestamo = prestamosVigentes
                .FirstOrDefault(p => p.TipoPrestamo == tipo.ClavePrestamo);

            decimal saldo = 0;
            DateTime? fechaPrestamo = null;
            DateTime? fechaVencimiento = null;
            decimal importePrestamo = 0;

            if (prestamo != null)
            {
                saldo = prestamo.SaldoPrestamo;
                fechaPrestamo = prestamo.FechaPrestamo;
                fechaVencimiento = prestamo.FechaVencimiento;
                importePrestamo = prestamo.ImportePagare;
            }

            decimal puedeSolicitar = (contexto.MisAhorros * tipo.VecesAhorro) - saldo;

            if (puedeSolicitar < 0)
                puedeSolicitar = 0;

            return new EstadoCuentaRowsDto
            {
                IdReporte = contexto.IdReporte,
                ClavePension = contexto.ClavePension,
                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = tipo.SubClave,
                NombrePrestamo = tipo.NombrePrestamo,
                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importePrestamo,
                PlazoMeses = tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,
                SaldoPrestamo = saldo,
                CantidadPuedeSolicitar = puedeSolicitar,
                ImporteLiquido = 0,
                Descuento = 0,
                LiquidaCon = 0
            };
        }
    }
}
