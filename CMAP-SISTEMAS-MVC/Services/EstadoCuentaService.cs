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
        public async Task<EstadoCuentaResultadoDTO> GenerarEstadoCuentaAsync(EstadoCuentaContextDto contexto)
        {
            ValidarContexto(contexto);

            await CargarDatosBaseSocioEnContextoAsync(contexto);

           
            var noPersonales = await _prestamoNoPersonalService
                .GenerarPrestamosNoPersonalesAsync(contexto);

            var personales = new List<EstadoCuentaRowsDto>();
            PrestamoPersonalResumenDTO? resumenPrestamoPersonal = null;

            if (!contexto.SoloPrestamoGM)
            {
                var resultadoPersonal = await _prestamoPersonalService
                    .GenerarPrestamosPersonalesAsync(contexto);

                personales = resultadoPersonal.FilasProyeccion;
                resumenPrestamoPersonal = resultadoPersonal.Resumen;
            }

            if (resumenPrestamoPersonal == null)
            {
                resumenPrestamoPersonal = new PrestamoPersonalResumenDTO
                {
                    TienePrestamoVigente = false,
                    PuedeRenovar = false,
                    CumplePago = false,
                    CumplePlazo = false,
                    DiasFaltantes = 0,
                    MontoFaltanteParaRenovar = 0,
                    SaldoPrestamoActivo = 0,
                    LiquidaConPrestamoActivo = 0,
                    MensajeResultado = "No se pudo determinar el estado del préstamo personal."
                };
            }


            var resultado = new EstadoCuentaResultadoDTO
            {
                Encabezado = await ConstruirEncabezadoAsync(contexto),
                PrestamosNoPersonales = noPersonales,
                PrestamosPersonales = personales,
                ResumenPrestamoPersonal = resumenPrestamoPersonal
            };

            return resultado;
        }

        private async Task<EstadoCuentaEncabezadoDTO> ConstruirEncabezadoAsync(EstadoCuentaContextDto ctx)
        {
            var socio = await _context.TABLA_DE_SOCIOS
                .AsNoTracking()
                .Where(s => s.ClavePension == ctx.ClavePension)
                .FirstOrDefaultAsync();

            if (socio == null)
            {
                return new EstadoCuentaEncabezadoDTO
                {
                    ClavePension = ctx.ClavePension,
                    FechaSistema = ctx.FechaSistema
                };
            }

            string nombreSocio = string.Join(" ",
                new[]
                {
            socio.ApellidosSocio?.Trim(),
            socio.NombreSocio?.Trim()
                }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return new EstadoCuentaEncabezadoDTO
            {
                ClavePension = socio.ClavePension ?? string.Empty,
                NombreSocio = nombreSocio,

                SaldoAhorros = socio.SaldoAhorros ?? 0m,
                AhorroMensual = socio.ULTAPORTA ?? 0m,
                SueldoLiquido = socio.SueldoNetoTotal ?? 0m,

                SaldoBono = socio.FIDEPROYECTADO ?? 0m,
                ApBonoMensual = socio.SaldoFide ?? 0m,
                LiquidezMinima = 0m,

                RemanenteIncluidoAhorros = socio.AhorroPendiente ?? 0m,
                AhorrosDiciembreAnterior = socio.SDOAHORROSINICIAL ?? 0m,

                ArchivoUbicacion = socio.ARCHIVO ?? string.Empty,
                NumeroExpediente = (socio.FOLIO ?? 0).ToString(),
                TipoSocio = ctx.Estatus,

                FechaSistema = ctx.FechaSistema
            };
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

            // ============================================================
            // CAMPOS NECESARIOS PARA REGLAS DE NEGOCIO
            // ============================================================

            if (!string.IsNullOrWhiteSpace(socio.Estatus))
                ctx.Estatus = socio.Estatus;

            ctx.MesesCot = CalcularMesesCotizados(socio.FechaIngreso, ctx.FechaSistema);

            ctx.Salario = socio.Salario;
            ctx.ElLimite = socio.LimiteDescuento;

            // ============================================================
            // NUEVO: saldo acumulado de préstamos topados por ahorro
            // Se usa para aplicar el límite global de 3.5 veces ahorro.
            // ============================================================
            ctx.SaldoPrestamosTopadosAhorro = await ObtenerSaldoPrestamosTopadosAhorroAsync(ctx.ClavePension);

            // ============================================================
            // NUEVO: bandera para excepción del tope global
            // Por default la dejamos en false.
            // Después puedes setearla desde el flujo que origine
            // una solicitud especial.
            // ============================================================
            ctx.EsSolicitudEspecial = false;

            // TODO(VB): aquí se derivan más banderas/estatus si aplica:
            // - descuentos generados
            // - fechas base/proyección de descuentos
            // - reglas heredadas por tipo de socio
        }

        private async Task<decimal> ObtenerSaldoPrestamosTopadosAhorroAsync(string clavePension)
        {
            var clavesControladas = new[] { "PP", "PR", "RE", "ES", "PC" };

            var saldo = await _context.TABLA_DE_PRESTAMOS
                .AsNoTracking()
                .Where(p => p.ClavePension == clavePension
                         && clavesControladas.Contains(p.TipoPrestamo)
                         && p.EstatusPrestamo == "VI")
                .SumAsync(p => (decimal?)p.SaldoPrestamo);

            return saldo ?? 0m;
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
                FechaIngreso = fechaIngreso,

                Estatus = socio.EstatusActualSocio ?? string.Empty,
                Salario = 0,
                LimiteDescuento = 0
            };
        }

        /* ============================================================
         * MÉTODOS AUXILIARES
        * ============================================================ */
        private static int CalcularMesesCotizados(DateTime? fechaIngreso, DateTime fechaSistema)
        {
            if (!fechaIngreso.HasValue)
                return 0;

            var inicio = fechaIngreso.Value.Date;
            var fin = fechaSistema.Date;

            if (fin < inicio)
                return 0;

            int meses = (fin.Year - inicio.Year) * 12 + fin.Month - inicio.Month;

            if (fin.Day < inicio.Day)
                meses--;

            return meses < 0 ? 0 : meses;
        }




    }
}



