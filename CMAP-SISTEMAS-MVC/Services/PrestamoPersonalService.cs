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

            /* ============================================================
            * CASO 1: HAY PRÉSTAMO PERSONAL VIGENTE
            * ============================================================
            * Regla VB:
            * - El PP vigente SIEMPRE debe mostrarse en la tabla general.
            * - Si puede renovar, además se muestran las proyecciones por modalidad.
            * - Si NO puede renovar, se conserva por lo menos una fila base
            *   para que EstadoCuentaService pueda agregar "PERSONAL" a
            *   Información de Préstamos.
            * ============================================================ */
            if (prestamoPP != null)
            {
                var resumen = ConstruirResumenPrestamoPersonal(contexto, tiposPP, prestamoPP);
                resultado.Resumen = resumen;

                var tipoBase = tiposPP
                    .OrderBy(x => x.SubCve)
                    .FirstOrDefault();

                if (tipoBase != null)
                {
                    if (resumen.PuedeRenovar)
                    {
                        // Fila PP informativa para la tabla 2: Información de Préstamos
                        var filaVigente = await ConstruirFilaPrestamoPersonalAsync(
                            contexto,
                            tipoBase,
                            prestamoPP);

                        if (filaVigente != null)
                        {
                            filaVigente.NombrePrestamo = "PERSONAL";
                            filaVigente.ClavePrestamo = "PP";
                            filaVigente.CantidadPuedeSolicitar = 0m;
                            filaVigente.ImporteLiquido = 0m;
                            filaVigente.EstaVigente = true;
                            filaVigente.EsProyeccion = false;
                            filaVigente.OrdenVisual = 30;

                            resultado.FilaPrestamoPPVigente = filaVigente;
                        }

                        // Proyección real para sección 3
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
                    else
                    {
                        var filaVigente = await ConstruirFilaPrestamoPersonalAsync(
                            contexto,
                            tipoBase,
                            prestamoPP);

                        if (filaVigente != null)
                        {
                            filaVigente.NombrePrestamo = "PERSONAL";
                            filaVigente.ClavePrestamo = "PP";
                            filaVigente.CantidadPuedeSolicitar = 0m;
                            filaVigente.ImporteLiquido = 0m;
                            filaVigente.EstaVigente = true;
                            filaVigente.EsProyeccion = false;
                            filaVigente.OrdenVisual = 30;

                            resultado.FilaPrestamoPPVigente = filaVigente;
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
            var estatus = (ctx.Estatus ?? string.Empty).Trim();
            var vigencia = (ctx.Vigencia ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(vigencia))
                throw new InvalidOperationException("No se encontró la vigencia del socio en el contexto.");

            return await (
                from tp in _context.TABLA_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                join dp in _context.DETALLE_DE_TIPOS_DE_PRESTAMOS.AsNoTracking()
                    on tp.ClavePrestamo equals dp.ClavePrestamo
                where dp.TipoSocio == estatus
                      && dp.Vigencia == vigencia
                      && tp.ClavePrestamo == "PP"
                      && dp.Vigente == "S"
                select new TipoPrestamoDto
                {
                    ClavePrestamo = tp.ClavePrestamo ?? string.Empty,

                    NombrePrestamo = !string.IsNullOrWhiteSpace(dp.NombrePrestamo)
                        ? dp.NombrePrestamo.Trim()
                        : (tp.NombrePrestamo ?? string.Empty).Trim(),

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
                         && (p.TipoPrestamo ?? "").Trim() == "PP"
                         && (p.EstatusPrestamo ?? "").Trim() == "VI")
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

            /* ============================================================
             * 1. Determinar si se permite calcular proyección PP
             * ------------------------------------------------------------
             * VB solo genera registros en TbledoctaPP cuando:
             *
             *   FALTAPAGAR = 0 And FALTAPLAZO = 0
             *
             * En C# esto se representa con puedeRenovar.
             * ============================================================ */
            if (!tienePrestamoVigente)
            {
                puedeRenovar = true;
            }
            else
            {
                var resumen = ConstruirResumenPrestamoPersonal(
                    ctx,
                    new List<TipoPrestamoDto> { tipo },
                    prestamoPP!);

                puedeRenovar = resumen.PuedeRenovar;

                /* ============================================================
                 * 1.1 Si NO puede renovar, solo se conserva información base
                 * ------------------------------------------------------------
                 * No se calcula:
                 *   - PuedeSolicitar
                 *   - ImporteLiquido
                 *
                 * El descuento se deja como la amortización vigente, si existe.
                 * ============================================================ */
                if (!puedeRenovar)
                {
                    puedeSolicitar = 0m;
                    importeLiquido = 0m;

                    descuento = prestamoPP!.ImporteAmortizacion > 0
                        ? prestamoPP.ImporteAmortizacion
                        : 0m;
                }
            }

            /* ============================================================
             * 2. Calcular proyección cuando sí puede renovar
             * ------------------------------------------------------------
             * Aplica para dos escenarios:
             *
             * A) No tiene préstamo PP vigente.
             * B) Sí tiene préstamo PP vigente, pero ya cumple pago y plazo.
             * ============================================================ */
            if (puedeRenovar)
            {
                /* ============================================================
                 * 2.1 Calcular amortización anterior
                 * ------------------------------------------------------------
                 * Replica VB:
                 *
                 * If RSTPrestamo!SaldoPrestamo > 0 Then
                 *    AmortAnt = IIf(
                 *        RSTPrestamo!SaldoPrestamo < RSTPrestamo!ImporteAmortizacion,
                 *        RSTPrestamo!SaldoPrestamo,
                 *        RSTPrestamo!ImporteAmortizacion)
                 * Else
                 *    AmortAnt = 0
                 * End If
                 * ============================================================ */
                decimal amortizacionAnterior = 0m;

                if (prestamoPP != null && prestamoPP.SaldoPrestamo > 0)
                {
                    amortizacionAnterior = prestamoPP.SaldoPrestamo < prestamoPP.ImporteAmortizacion
                        ? prestamoPP.SaldoPrestamo
                        : prestamoPP.ImporteAmortizacion;
                }

                /* ============================================================
                 * 2.2 Calcular Puede Solicitar
                 * ============================================================ */
                puedeSolicitar = CalcularAlcancePrestamoPersonal(
                    ctx,
                    tipo,
                    amortizacionAnterior);

                if (puedeSolicitar < 0)
                    puedeSolicitar = 0m;

                /* ============================================================
                 * 2.3 Calcular descuento
                 * ============================================================ */
                descuento = CalcularDescuentoPrestamoPersonal(
                    ctx,
                    tipo,
                    puedeSolicitar);

                /* ============================================================
                 * 2.4 Calcular importe líquido con lógica PV
                 * ============================================================ */
                importeLiquido = await CalcularImporteLiquidoPrestamoPersonalAsync(
                    ctx,
                    tipo,
                    prestamoPP,
                    puedeSolicitar);

                /* ============================================================
                 * 2.5 Si el líquido queda en cero, también dejar descuento en cero
                 * ------------------------------------------------------------
                 * VB inserta:
                 *
                 *   Importeliquido = IIf(tbImporteLiquido < 0, 0, tbImporteLiquido)
                 *   Descuentos     = IIf(tbImporteLiquido < 0, 0, tbDescuento)
                 *
                 * Aquí usamos <= 0 para evitar mostrar descuento si no hay líquido útil.
                 * ============================================================ */
                if (importeLiquido <= 0)
                {
                    importeLiquido = 0m;
                    descuento = 0m;
                }
            }

            /* ============================================================
             * 3. Construir fila final del estado de cuenta
             * ============================================================ */
            return new EstadoCuentaRowsDto
            {
                IdReporte = ctx.IdReporte,
                ClavePension = ctx.ClavePension,

                ClavePrestamo = tipo.ClavePrestamo,
                SubClave = tipo.SubCve,
                NombrePrestamo = (tipo.NombrePrestamo ?? string.Empty).Trim(),

                FechaPrestamo = fechaPrestamo,
                ImportePrestamo = importePrestamo,
                PlazoMeses = prestamoPP?.NumMesesPrestamo ?? tipo.PlazoMaximo,
                FechaVencimiento = fechaVencimiento,

                SaldoPrestamo = saldoPrestamo,
                CantidadPuedeSolicitar = puedeSolicitar,
                ImporteLiquido = importeLiquido,
                Descuento = descuento,
                LiquidaCon = liquidaCon,
                TasaInteres = tipo.TasaIntNormal
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
            var resumen = ConstruirResumenPrestamoPersonal(
                ctx,
                new List<TipoPrestamoDto> { tipo },
                prestamoPP);

            return resumen.PuedeRenovar;
        }

       

        /* ============================================================
        * SECCIÓN D.1: CÁLCULO DEL ALCANCE DE PRÉSTAMO PERSONAL
        * ------------------------------------------------------------
        * Replica la lógica base de VB en NuevaAgregaPersonales():
        *
        * 1. Calcula el alcance por sueldo.
        * 2. Calcula el alcance por ahorros.
        * 3. Toma el menor de ambos.
        * 4. Aplica el tope global de PP.
        *
        * Fórmula VB equivalente:
        *   AlcanceSueldo = Round((TOTSUELDO - Ellimite + AmortAnt) * TMPnumerodePagos, 2)
         *   tbPuedeSolicitar = Round(MisAhorros * FactorSobreAhorro, 2)
        *   If AlcanceSueldo < tbPuedeSolicitar Then tbPuedeSolicitar = AlcanceSueldo
        * ============================================================ */
        private decimal CalcularAlcancePrestamoPersonal(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal amortizacionAnterior)
        {
            /* ------------------------------------------------------------
             * 1. Determinar número de pagos
             * ------------------------------------------------------------
             * En VB:
             *   TMPnumerodePagos = IIf(ESTATUS = "A", plazomaximo * 2, plazomaximo)
             *
             * Activo:
             *   El plazo mensual se convierte a pagos quincenales.
             *
             * Jubilado:
             *   El plazo se conserva como pagos mensuales.
             * ------------------------------------------------------------ */
            int numeroPagos = CalcularNumeroPagos(ctx, tipo);

            /* ------------------------------------------------------------
             * 2. Calcular alcance por sueldo
             * ------------------------------------------------------------
             * Fórmula VB:
             *   AlcanceSueldo = Round((TOTSUELDO - Ellimite + AmortAnt) * TMPnumerodePagos, 2)
             *
             * Donde:
             *   TotSueldo             = sueldo disponible del socio.
             *   ElLimite              = límite mínimo de liquidez.
             *   amortizacionAnterior  = descuento que se libera si renueva PP.
             *   numeroPagos           = plazo real de pago según estatus.
             * ------------------------------------------------------------ */
            decimal alcanceSueldo = Math.Round(
                (ctx.TotSueldo - ctx.ElLimite + amortizacionAnterior) * numeroPagos,
                2
            );

            /* ------------------------------------------------------------
             * 3. Calcular alcance por ahorros
             * ------------------------------------------------------------
             * Fórmula VB:
             *   tbPuedeSolicitar = Round(MisAhorros * FactorSobreAhorro, 2)
             *
             * Importante:
             *   Se usa FactorSobreAhorro desde DETALLE_DE_TIPOS_DE_PRESTAMOS.
             *   No se hardcodea 2.41 / 1.85 aquí, porque la modalidad PP
             *   debe venir configurada desde base de datos.
             * ------------------------------------------------------------ */
            decimal alcanceAhorros = Math.Round(
                ctx.MisAhorros * tipo.FactorSobreAhorro,
                2
            );

            /* ------------------------------------------------------------
             * 4. Elegir el menor alcance
             * ------------------------------------------------------------
             * VB compara alcance por sueldo contra alcance por ahorros.
             * El socio solo puede solicitar hasta el menor de los dos.
             * ------------------------------------------------------------ */
            decimal puedeSolicitar = Math.Min(alcanceSueldo, alcanceAhorros);

            /* ------------------------------------------------------------
             * 5. Evitar montos negativos
             * ------------------------------------------------------------
             * Si por sueldo, límite o datos de contexto el cálculo queda
             * negativo, se fuerza a cero.
             * ------------------------------------------------------------ */
            if (puedeSolicitar < 0)
                puedeSolicitar = 0m;

            /* ------------------------------------------------------------
             * 6. Calcular tope global PP
             * ------------------------------------------------------------
             * En VB:
             *   VECESPP
             *
             * Aquí se calcula temporalmente como:
             *   MisAhorros * FactorSobreAhorro
             *
             * Nota:
             *   Si después tienes VECESPP ya calculado en EstadoCuentaContextDto,
             *   conviene usar ctx.VecesPP en lugar de recalcularlo aquí.
             * ------------------------------------------------------------ */
            decimal vecesPp = ctx.MisAhorros * tipo.FactorSobreAhorro;

            /* ------------------------------------------------------------
             * 7. Aplicar límite global de PP
             * ------------------------------------------------------------
             * Fórmula VB:
             *   If (tbPuedeSolicitar + SALDOP - SDOPRESTAMOPP) > VECESPP
             *      And (VECESPP > 0) Then
             *
             *      tbPuedeSolicitar =
             *          tbPuedeSolicitar -
             *          ((tbPuedeSolicitar + SALDOP - SDOPRESTAMOPP) - VECESPP)
             *   End If
             *
             * Donde:
             *   SaldoP         = saldo total de préstamos.
             *   SdoPrestamoPP  = saldo del préstamo personal actual.
             *   vecesPp        = límite máximo permitido para PP.
             * ------------------------------------------------------------ */
            if (vecesPp > 0 && (puedeSolicitar + ctx.SaldoP - ctx.SdoPrestamoPP) > vecesPp)
            {
                puedeSolicitar -= (puedeSolicitar + ctx.SaldoP - ctx.SdoPrestamoPP) - vecesPp;
            }

            /* ------------------------------------------------------------
             * 8. Revalidar monto negativo después del tope
             * ------------------------------------------------------------
             * El ajuste por VECESPP puede reducir el monto por debajo de cero.
             * En ese caso, se fuerza nuevamente a cero.
             * ------------------------------------------------------------ */
            if (puedeSolicitar < 0)
                puedeSolicitar = 0m;

            /* ------------------------------------------------------------
             * 9. Retornar monto final redondeado
             * ------------------------------------------------------------
             * Este valor corresponde a:
             *   tbPuedeSolicitar / CantidadPuedeSolicitar
             * ------------------------------------------------------------ */
            return Math.Round(puedeSolicitar, 2);
        }

        /* ============================================================
         * SECCIÓN D.2: CÁLCULO DEL DESCUENTO DE PRÉSTAMO PERSONAL
         * ------------------------------------------------------------
         * Replica la fórmula VB:
         *
         *   tbDescuento = Round(tbPuedeSolicitar / TMPnumerodePagos, 2)
         *
         * El descuento se calcula sobre el pagaré/puede solicitar,
         * no sobre el importe líquido.
         * ============================================================ */
        private decimal CalcularDescuentoPrestamoPersonal(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            decimal puedeSolicitar)
        {
            /* ------------------------------------------------------------
             * 1. Determinar número de pagos
             * ------------------------------------------------------------
             * Activos:
             *   PlazoMaximo * 2
             *
             * Jubilados:
             *   PlazoMaximo
             * ------------------------------------------------------------ */
            int numeroPagos = CalcularNumeroPagos(ctx, tipo);

            /* ------------------------------------------------------------
             * 2. Validar número de pagos
             * ------------------------------------------------------------
             * Si no existe plazo válido, no se puede calcular descuento.
             * ------------------------------------------------------------ */
            if (numeroPagos <= 0)
                return 0m;

            /* ------------------------------------------------------------
             * 3. Calcular descuento
             * ------------------------------------------------------------
             * Fórmula VB:
             *   tbDescuento = Round(tbPuedeSolicitar / TMPnumerodePagos, 2)
             * ------------------------------------------------------------ */
            return Math.Round(puedeSolicitar / numeroPagos, 2);
        }

        /* ============================================================
         * SECCIÓN D.3: CÁLCULO DEL IMPORTE LÍQUIDO DE PP
         * ------------------------------------------------------------
         * Replica el flujo principal de VB:
         *
         * 1. Quitar seguro pasivo mediante factor.
         * 2. Restar fondo de garantía.
         * 3. Calcular amortización parcial.
         * 4. Calcular valor presente PV.
         * 5. Calcular interés adicional por días.
         * 6. Restar préstamo anterior.
         *
         * Fórmula final VB:
         *   tbImporteLiquido = Solicitar - PtmoAnterior - intadic
         *
         * Donde Solicitar ya fue recalculado con PV.
         * ============================================================ */
        private async Task<decimal> CalcularImporteLiquidoPrestamoPersonalAsync(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo,
            PrestamoVigenteDto? prestamoPP,
            decimal puedeSolicitar)
        {
            /* ------------------------------------------------------------
             * 1. Validar monto base
             * ------------------------------------------------------------
             * Si no hay cantidad posible a solicitar, no existe líquido.
             * ------------------------------------------------------------ */
            if (puedeSolicitar <= 0)
                return 0m;

            /* ------------------------------------------------------------
             * 2. Determinar número de pagos
             * ------------------------------------------------------------
             * Este valor equivale a TMPnumerodePagos en VB.
             * ------------------------------------------------------------ */
            int numeroPagos = CalcularNumeroPagos(ctx, tipo);

            if (numeroPagos <= 0)
                return 0m;

            /* ------------------------------------------------------------
             * 3. Quitar seguro pasivo mediante factor
             * ------------------------------------------------------------
             * Fórmula VB:
             *   FactorFondo = 1 + (PorcenSeguroPasivo / 100)
             *   Solicitar = Round(tbPuedeSolicitar / FactorFondo, 2)
             *
             * Nota:
             *   Aunque la variable VB se llama FactorFondo, en realidad
             *   aquí se está usando el porcentaje de seguro pasivo.
             * ------------------------------------------------------------ */
            decimal factorSeguro = 1m + (tipo.PorcenSeguroPasivo / 100m);

            decimal solicitar = factorSeguro > 0
                ? Math.Round(puedeSolicitar / factorSeguro, 2)
                : puedeSolicitar;

            /* ------------------------------------------------------------
             * 4. Restar fondo de garantía
             * ------------------------------------------------------------
             * Fórmula VB:
             *   Solicitar = Solicitar - FondoGarantia(...)
             *
             * IMPORTANTE:
             *   Esta implementación usa PorcenFondoGarantia como aproximación.
             *   Para empatar exactamente con VB, aquí debe conectarse la rutina
             *   legacy FondoGarantia("PP", plazoMaximo, ESTATUS, Solicitar, MisAhorros).
             * ------------------------------------------------------------ */
            decimal fondoGarantia = Math.Round(
                solicitar * (tipo.PorcenFondoGarantia / 100m),
                2
            );

            solicitar -= fondoGarantia;

            /* ------------------------------------------------------------
             * 5. Calcular amortización parcial
             * ------------------------------------------------------------
             * Fórmula VB:
             *   AmortizacionParcial = Solicitar / TMPnumerodePagos
             *
             * Esta amortización se usa como pago periódico para calcular PV.
             * ------------------------------------------------------------ */
            decimal amortizacionParcial = solicitar / numeroPagos;

            /* ------------------------------------------------------------
             * 6. Determinar tasa por periodo
             * ------------------------------------------------------------
             * Fórmula VB:
             *   Si ESTATUS = "J":
             *      nTasaPer = TasaIntNormal / 1200
             *   Si no:
             *      nTasaPer = TasaIntNormal / 2400
             *
             * Jubilado:
             *   tasa mensual.
             *
             * Activo:
             *   tasa quincenal.
             * ------------------------------------------------------------ */
            decimal tasaPeriodo = (ctx.Estatus ?? string.Empty).Trim() == "J"
                ? tipo.TasaIntNormal / 1200m
                : tipo.TasaIntNormal / 2400m;

            /* ------------------------------------------------------------
             * 7. Calcular valor presente del préstamo
             * ------------------------------------------------------------
             * Fórmula VB:
             *   Solicitar = Round(Abs(PV(nTasaPer, TMPnumerodePagos, AmortizacionParcial)), 2)
             *
             * Este paso recalcula el importe base real del préstamo antes
             * de restar préstamo anterior e interés adicional.
             * ------------------------------------------------------------ */
            decimal solicitarPv = Math.Round(
                Math.Abs(CalcularPV(tasaPeriodo, numeroPagos, amortizacionParcial)),
                2
            );

            /* ------------------------------------------------------------
             * 8. Calcular días adicionales
             * ------------------------------------------------------------
             * En VB:
             *   DiasAdic depende de FechaPrimerPago(...)
             *
             * Por ahora este helper devuelve 0 hasta conectar la lógica real.
             * ------------------------------------------------------------ */
            int diasAdic = CalcularDiasAdicPrestamoPersonal(ctx);

            /* ------------------------------------------------------------
             * 9. Calcular interés adicional por días
             * ------------------------------------------------------------
             * Fórmula VB:
             *   intadic = Round(((Solicitar * TasaIntNormal / 100) / 360) * DiasAdic, 2)
             *
             * Solo aplica cuando DiasAdic > 0.
             * ------------------------------------------------------------ */
            decimal interesAdicional = 0m;

            if (diasAdic > 0)
            {
                interesAdicional = Math.Round(
                    ((solicitarPv * tipo.TasaIntNormal / 100m) / 360m) * diasAdic,
                    2
                );
            }

            /* ------------------------------------------------------------
             * 10. Determinar préstamo anterior a liquidar
             * ------------------------------------------------------------
             * Fórmula VB real:
             *   PtmoAnterior = SaldoPrestamo - BonAnterior - BonSegAnt + MoraAnterior
             *
             * Temporal:
             *   Mientras no estén conectadas:
             *     - BonificaIntereses
             *     - BonificaSeguroPasivo
             *     - fu_calcular_moratorios
             *
             *   Se usa LiquidaCon si ya viene calculado; si no, SaldoPrestamo.
             * ------------------------------------------------------------ */
            decimal prestamoAnterior = 0m;

            if (prestamoPP != null && prestamoPP.SaldoPrestamo > 0)
            {
                prestamoAnterior = prestamoPP.LiquidaCon > 0
                    ? prestamoPP.LiquidaCon
                    : prestamoPP.SaldoPrestamo;
            }

            /* ------------------------------------------------------------
             * 11. Calcular importe líquido final
             * ------------------------------------------------------------
             * Fórmula VB:
             *   tbImporteLiquido = Solicitar - PtmoAnterior - intadic
             *
             * En C#:
             *   Solicitar = solicitarPv
             *   PtmoAnterior = prestamoAnterior
             *   intadic = interesAdicional
             * ------------------------------------------------------------ */
            decimal importeLiquido = solicitarPv - prestamoAnterior - interesAdicional;

            /* ------------------------------------------------------------
             * 12. Evitar líquido negativo
             * ------------------------------------------------------------
             * VB inserta 0 cuando tbImporteLiquido queda negativo.
             * ------------------------------------------------------------ */
            if (importeLiquido < 0)
                importeLiquido = 0m;

            await Task.CompletedTask;

            /* ------------------------------------------------------------
             * 13. Retornar líquido final redondeado
             * ------------------------------------------------------------ */
            return Math.Round(importeLiquido, 2);
        }

        /* ============================================================
         * SECCIÓN D.4: CÁLCULO DEL NÚMERO DE PAGOS
         * ------------------------------------------------------------
         * Equivalente VB:
         *
         *   TMPnumerodePagos = IIf(ESTATUS = "A",
         *                          plazomaximo * 2,
         *                          plazomaximo)
         *
         * Activos pagan quincenalmente.
         * Jubilados pagan mensualmente.
         * ============================================================ */
        private static int CalcularNumeroPagos(
            EstadoCuentaContextDto ctx,
            TipoPrestamoDto tipo)
        {
            return (ctx.Estatus ?? string.Empty).Trim() == "A"
                ? tipo.PlazoMaximo * 2
                : tipo.PlazoMaximo;
        }

        /* ============================================================
         * SECCIÓN D.5: CÁLCULO DE VALOR PRESENTE
         * ------------------------------------------------------------
         * Replica el comportamiento financiero de PV usado en VB:
         *
         *   PV(nTasaPer, TMPnumerodePagos, AmortizacionParcial)
         *
         * Fórmula:
         *   PV = PMT * (1 - (1 + tasa)^-n) / tasa
         *
         * Donde:
         *   tasaPeriodo = tasa por periodo.
         *   numeroPagos = cantidad de pagos.
         *   pago        = amortización parcial.
         * ============================================================ */
        private static decimal CalcularPV(
            decimal tasaPeriodo,
            int numeroPagos,
            decimal pago)
        {
            /* ------------------------------------------------------------
             * 1. Validar número de pagos
             * ------------------------------------------------------------ */
            if (numeroPagos <= 0)
                return 0m;

            /* ------------------------------------------------------------
             * 2. Caso sin interés
             * ------------------------------------------------------------
             * Si la tasa es 0, el valor presente equivale al total pagado.
             * ------------------------------------------------------------ */
            if (tasaPeriodo == 0)
                return pago * numeroPagos;

            /* ------------------------------------------------------------
             * 3. Convertir a double para usar Math.Pow
             * ------------------------------------------------------------
             * Math.Pow trabaja con double.
             * El resultado se convierte nuevamente a decimal.
             * ------------------------------------------------------------ */
            double tasa = (double)tasaPeriodo;
            double pmt = (double)pago;

            /* ------------------------------------------------------------
             * 4. Calcular valor presente
             * ------------------------------------------------------------
             * Fórmula:
             *   PV = PMT * (1 - (1 + tasa)^-n) / tasa
             * ------------------------------------------------------------ */
            double pv = pmt * (1 - Math.Pow(1 + tasa, -numeroPagos)) / tasa;

            return (decimal)pv;
        }

        /* ============================================================
        * SECCIÓN D.6: CÁLCULO TEMPORAL DE DÍAS ADICIONALES
        * ------------------------------------------------------------
        * En VB, DiasAdic depende de FechaPrimerPago(...).
        *
        * Por ahora se deja en cero para no alterar el cálculo mientras
        * no esté conectada la lógica legacy de primer pago.
         * ============================================================ */
        private static int CalcularDiasAdicPrestamoPersonal(
            EstadoCuentaContextDto ctx)
        {
            return 0;
        }

      
       
    }
}