using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Services
{
    /// <summary>
    /// ============================================================
    /// SERVICIO: EstadoCuentaService
    /// ------------------------------------------------------------
    /// Genera el Estado de Cuenta (resumen) para un socio.
    ///
    /// Migración VB → C# (referencia):
    ///  - ActualizaTablaTemporalReporte()  -> préstamos NO personales
    ///  - NuevaAgregaPersonales()          -> préstamo personal (PP)
    ///
    /// NOTA:
    ///  - Este servicio trabaja con DTOs (no ViewModels).
    ///  - La vista debe consumir lo que entregue el controller.
    /// ============================================================
    /// </summary>
    public class EstadoCuentaService
    {
        /* ============================================================
         * CAMPOS PRIVADOS
         * ============================================================ */
        private readonly Cmap54SistemasContext _context;
        private readonly IPrestamoNoPersonalService _prestamoNoPersonalService;
        private readonly IPrestamoPersonalService _prestamoPersonalService;

        /* ============================================================
         * CONSTRUCTOR
         * ============================================================ */
        public EstadoCuentaService(Cmap54SistemasContext context,
               IPrestamoNoPersonalService prestamoNoPersonalService,
               IPrestamoPersonalService prestamoPersonalService)
        {
            _context = context;
            _prestamoNoPersonalService = prestamoNoPersonalService;
            _prestamoPersonalService = prestamoPersonalService;
        }

        /* ============================================================
         * API PÚBLICA
         * ============================================================ */

        /// <summary>
        /// Genera el estado de cuenta (resumen por tipo de préstamo).
        /// </summary>
        public async Task<List<EstadoCuentaRowsDto>> GenerarEstadoCuentaAsync(EstadoCuentaContextDto contexto)
        {
            ValidarContexto(contexto);

            await CargarDatosBaseSocioEnContextoAsync(contexto);

            var noPersonales = await _prestamoNoPersonalService
                .GenerarPrestamosNoPersonalesAsync(contexto);

            var personales = await _prestamoPersonalService
                .GenerarPrestamosPersonalesAsync(contexto);

            var resultado = new List<EstadoCuentaRowsDto>();
            resultado.AddRange(noPersonales);
            resultado.AddRange(personales);

            return resultado;
        }

        /* ============================================================
         * VALIDACIONES
         * ============================================================ */

        /// <summary>
        /// Valida los datos mínimos requeridos para generar el estado de cuenta.
        /// </summary>
        private static void ValidarContexto(EstadoCuentaContextDto contexto)
        {
            if (contexto == null)
                throw new ArgumentNullException(nameof(contexto));

            if (string.IsNullOrWhiteSpace(contexto.ClavePension))
                throw new ArgumentException("La ClavePension es obligatoria.", nameof(contexto.ClavePension));
        }

        /* ============================================================
         * SECCIÓN A: SOCIO BASE (TABLA_DE_SOCIOS)
         * ============================================================ */

        /// <summary>
        /// Lee TABLA_DE_SOCIOS y llena campos del contexto necesarios para cálculos.
        /// </summary>
        private async Task CargarDatosBaseSocioEnContextoAsync(EstadoCuentaContextDto ctx)
        {
            var socio = await ObtenerSocioBaseAsync(ctx.ClavePension);

            if (socio == null)
                return;

            ctx.MisAhorros = socio.SaldoAhorros;
            ctx.TotSueldo = socio.SueldoNetoTotal;
            ctx.SaldoP = socio.SaldoPrestamos;
            ctx.FechaIngreso = socio.FechaIngreso;

            if (!string.IsNullOrWhiteSpace(socio.Vigencia))
                ctx.Vigencia = socio.Vigencia;

            // TODO(VB): aquí se derivan más banderas/estatus si aplica:
            // - ctx.Estatus
            // - MesesCot
            // - ElLimite
            // - Salario
            // - descuentos generados
        }

        /// <summary>
        /// Obtiene los datos base del socio desde TABLA_DE_SOCIOS.
        /// </summary>
        private async Task<SocioBaseDto?> ObtenerSocioBaseAsync(string clavePension)
        {
            var socio = await _context.TABLA_DE_SOCIOS
         .AsNoTracking()
         .Where(s => s.ClavePension == clavePension)
         .FirstOrDefaultAsync();

            if (socio == null)
                return null;

            DateTime? fechaIngreso = null;

            var estatus = socio.ClavePension?.Trim().Substring(0, 1);

            if (estatus == "1")
            {
                fechaIngreso = socio.FechaAltaActivo ?? socio.FechaAltaJubiladoPen;
            }
            else if (estatus == "2")
            {
                fechaIngreso = socio.FechaAltaJubiladoPen ?? socio.FechaAltaActivo;
            }
            else
            {
                fechaIngreso = socio.FechaAltaActivo ?? socio.FechaAltaJubiladoPen;
            }

            return new SocioBaseDto
            {
                ClavePension = socio.ClavePension ?? string.Empty,
                SaldoAhorros = socio.SaldoAhorros ?? 0,
                SueldoNetoTotal = socio.SueldoNetoTotal ?? 0,
                SaldoPrestamos = socio.SaldoPrestamos ?? 0,
                Vigencia = socio.VIGENCIA,
                Situacion = socio.SITUACION,
                FechaIngreso = fechaIngreso
            };
        }

       

      
    }
}



