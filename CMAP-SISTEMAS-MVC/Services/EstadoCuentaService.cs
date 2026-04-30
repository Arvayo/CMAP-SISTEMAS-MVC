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
    /// Genera el Estado de Cuenta para un socio.
    ///
    /// Referencia VB:
    ///  - ActualizaTablaTemporalReporte()  -> Préstamos NO personales
    ///  - NuevaAgregaPersonales()          -> Préstamo personal PP
    ///
    /// Responsabilidad:
    ///  - Cargar datos base del socio
    ///  - Generar préstamos no personales
    ///  - Generar préstamo personal PP
    ///  - Integrar PP vigente en tabla general
    ///  - Separar proyección PP de préstamo PP vigente
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

        public EstadoCuentaService(
            Cmap54SistemasContext context,
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

        public async Task<EstadoCuentaResultadoDTO> GenerarEstadoCuentaAsync(
            EstadoCuentaContextDto contexto)
        {
            ValidarContexto(contexto);

            await CargarDatosBaseSocioEnContextoAsync(contexto);

            /* ========================================================
             * SECCIÓN A: PRÉSTAMOS NO PERSONALES
             * --------------------------------------------------------
             * Equivalente VB:
             *  - ActualizaTablaTemporalReporte()
             *
             * Esta lista alimenta la tabla general:
             *  - ES
             *  - PC
             *  - EV
             *  - PR
             *  - RE
             *  - VI / VA
             *  - GM / EX / PH si aplican
             *
             * NOTA:
             *  PP no se calcula aquí. PP se integra después desde el
             *  servicio de préstamos personales.
             * ======================================================== */

            var noPersonales = await _prestamoNoPersonalService
                .GenerarPrestamosNoPersonalesAsync(contexto);

            /* ========================================================
             * SECCIÓN B: PRÉSTAMO PERSONAL PP
             * --------------------------------------------------------
             * Equivalente VB:
             *  - NuevaAgregaPersonales()
             *
             * Separación importante:
             *
             *  resultadoPersonal.FilaPrestamoPPVigente
             *      -> representa el PP activo real.
             *      -> se usa para mostrar PERSONAL en la tabla general.
             *
             *  resultadoPersonal.FilasProyeccion
             *      -> representa proyecciones de PP.
             *      -> solo debe alimentar la tabla inferior de PP.
             *
             * Esto evita que un PP vigente NO renovable aparezca como
             * proyección con importes en cero.
             * ======================================================== */

            var personales = new List<EstadoCuentaRowsDto>();
            PrestamoPersonalResumenDTO? resumenPrestamoPersonal = null;
            ResultadoPrestamoPersonalDto? resultadoPersonal = null;

            if (!contexto.SoloPrestamoGM)
            {
                resultadoPersonal = await _prestamoPersonalService
                    .GenerarPrestamosPersonalesAsync(contexto);

                personales = resultadoPersonal.FilasProyeccion;
                resumenPrestamoPersonal = resultadoPersonal.Resumen;
            }

            /* ========================================================
             * SECCIÓN C: FILA RESUMEN PP EN TABLA GENERAL
             * --------------------------------------------------------
             * El sistema VB muestra PERSONAL dentro de la tabla general
             * de "Información de Préstamos".
             *
             * Regla aplicada:
             *
             *  1. Si existe PP vigente:
             *      usar FilaPrestamoPPVigente.
             *
             *  2. Si no existe PP vigente, pero hay proyección:
             *      usar la primera fila de FilasProyeccion.
             *
             * Resultado:
             *  - PP vigente NO renovable:
             *      aparece en tabla general.
             *      NO aparece en tabla inferior de proyección.
             *
             *  - PP renovable:
             *      aparece en tabla general.
             *      puede aparecer en tabla inferior.
             * ======================================================== */

            var filaResumenPP =
                resultadoPersonal?.FilaPrestamoPPVigente
                ?? personales
                    .OrderBy(x => x.SubClave)
                    .FirstOrDefault();

            if (filaResumenPP != null)
            {
                noPersonales.Add(new EstadoCuentaRowsDto
                {
                    IdReporte = filaResumenPP.IdReporte,
                    ClavePension = filaResumenPP.ClavePension,

                    ClavePrestamo = "PP",
                    SubClave = filaResumenPP.SubClave,
                    NombrePrestamo = "PERSONAL",

                    FechaPrestamo = filaResumenPP.FechaPrestamo,
                    ImportePrestamo = filaResumenPP.ImportePrestamo,
                    PlazoMeses = filaResumenPP.PlazoMeses,
                    FechaVencimiento = filaResumenPP.FechaVencimiento,

                    SaldoPrestamo = filaResumenPP.SaldoPrestamo,
                    LiquidaCon = filaResumenPP.LiquidaCon,
                    CantidadPuedeSolicitar = filaResumenPP.CantidadPuedeSolicitar,
                    ImporteLiquido = filaResumenPP.ImporteLiquido,
                    Descuento = filaResumenPP.Descuento,

                    EstaVigente = filaResumenPP.EstaVigente,
                    EsProyeccion = filaResumenPP.EsProyeccion,
                    OrdenVisual = 3
                });
            }

            noPersonales = noPersonales
                .OrderBy(x => x.OrdenVisual)
                .ThenBy(x => x.SubClave)
                .ToList();

            resumenPrestamoPersonal ??= CrearResumenPrestamoPersonalDefault();

            return new EstadoCuentaResultadoDTO
            {
                Encabezado = await ConstruirEncabezadoAsync(contexto),
                PrestamosNoPersonales = noPersonales,
                PrestamosPersonales = personales,
                ResumenPrestamoPersonal = resumenPrestamoPersonal
            };
        }

        /* ============================================================
         * SECCIÓN D: ENCABEZADO DEL ESTADO DE CUENTA
         * ============================================================ */

        private async Task<EstadoCuentaEncabezadoDTO> ConstruirEncabezadoAsync(
            EstadoCuentaContextDto ctx)
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
         * SECCIÓN E: CARGA DE DATOS BASE DEL SOCIO
         * ============================================================ */

        private async Task CargarDatosBaseSocioEnContextoAsync(
            EstadoCuentaContextDto ctx)
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

            if (!string.IsNullOrWhiteSpace(socio.Estatus))
                ctx.Estatus = socio.Estatus;

            ctx.MesesCot = CalcularMesesCotizados(
                socio.FechaIngreso,
                ctx.FechaSistema);

            ctx.Salario = socio.Salario;
            ctx.ElLimite = socio.LimiteDescuento;

            /*
             * Regla general de préstamos:
             * --------------------------------------------------------
             * Saldo acumulado de préstamos topados por ahorro.
             *
             * Se utiliza para aplicar el límite global:
             *  - ningún préstamo debe exceder 3.5 veces los ahorros
             *    considerando saldos vigentes relacionados.
             */

            ctx.SaldoPrestamosTopadosAhorro =
                await ObtenerSaldoPrestamosTopadosAhorroAsync(ctx.ClavePension);

            /*
             * Por default se considera que NO es solicitud especial.
             * Si más adelante existe un flujo específico de solicitud
             * especial, esta bandera puede establecerse antes del cálculo.
             */

            ctx.EsSolicitudEspecial = false;
        }

        private async Task<SocioBaseDto?> ObtenerSocioBaseAsync(
            string clavePension)
        {
            var socio = await _context.TABLA_DE_SOCIOS
                .AsNoTracking()
                .Where(s => s.ClavePension == clavePension)
                .FirstOrDefaultAsync();

            if (socio == null)
                return null;

            DateTime? fechaIngreso = null;

            string estatus = socio.ClavePension?.Trim().Substring(0, 1) ?? string.Empty;

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
                SaldoAhorros = socio.SaldoAhorros ?? 0m,
                SueldoNetoTotal = socio.SueldoNetoTotal ?? 0m,
                SaldoPrestamos = socio.SaldoPrestamos ?? 0m,
                Vigencia = socio.VIGENCIA,
                Situacion = socio.SITUACION,
                FechaIngreso = fechaIngreso,

                Estatus = socio.EstatusActualSocio ?? string.Empty,
                Salario = 0m,
                LimiteDescuento = 0m
            };
        }

        private async Task<decimal> ObtenerSaldoPrestamosTopadosAhorroAsync(
            string clavePension)
        {
            var clavesControladas = new[] { "PP", "PR", "RE", "ES", "PC" };

            var saldo = await _context.TABLA_DE_PRESTAMOS
                .AsNoTracking()
                .Where(p =>
                    p.ClavePension == clavePension &&
                    clavesControladas.Contains(p.TipoPrestamo) &&
                    p.EstatusPrestamo == "VI")
                .SumAsync(p => (decimal?)p.SaldoPrestamo);

            return saldo ?? 0m;
        }

        /* ============================================================
         * SECCIÓN F: VALIDACIONES
         * ============================================================ */

        private static void ValidarContexto(
            EstadoCuentaContextDto contexto)
        {
            if (contexto == null)
                throw new ArgumentNullException(nameof(contexto));

            if (string.IsNullOrWhiteSpace(contexto.ClavePension))
            {
                throw new ArgumentException(
                    "La ClavePension es obligatoria.",
                    nameof(contexto.ClavePension));
            }
        }

        /* ============================================================
         * SECCIÓN G: AUXILIARES
         * ============================================================ */

        private static PrestamoPersonalResumenDTO CrearResumenPrestamoPersonalDefault()
        {
            return new PrestamoPersonalResumenDTO
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

        private static int CalcularMesesCotizados(
            DateTime? fechaIngreso,
            DateTime fechaSistema)
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



