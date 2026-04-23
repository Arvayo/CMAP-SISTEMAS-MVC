using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Services
{
    /// <summary>
    /// ============================================================
    /// SERVICIO: PrestamoCalculatorService
    /// ------------------------------------------------------------
    /// Encapsula cálculos reutilizables y llamadas a funciones SQL
    /// relacionadas con préstamos.
    /// ============================================================
    /// </summary>
    public class PrestamoCalculatorService : IPrestamoCalculatorService
    {
        private readonly Cmap54SistemasContext _context;

        public PrestamoCalculatorService(Cmap54SistemasContext context)
        {
            _context = context;
        }

        /* ============================================================
         * FUNCIONES SQL
         * ============================================================ */

        public async Task<decimal> ObtenerLiquidaConAsync(decimal idPrestamo, DateTime fecha)
        {
            const string sql = @"
                SELECT CAST(dbo.fu_liquidacon(@p0, @p1) AS DECIMAL(18,2)) AS Valor";

            return await EjecutarFuncionDecimalAsync(sql, idPrestamo, fecha.Date);
        }

        public async Task<decimal> ObtenerMoratoriosAsync(decimal idPrestamo, DateTime fecha)
        {
            const string sql = @"
                SELECT CAST(dbo.fu_calcular_moratorios(@p0, @p1) AS DECIMAL(18,2)) AS Valor";

            return await EjecutarFuncionDecimalAsync(sql, idPrestamo, fecha.Date);
        }

        /* ============================================================
         * CÁLCULOS GENERALES
         * ============================================================ */

        /// <summary>
        /// Devuelve el porcentaje cubierto de un préstamo.
        /// Ejemplo:
        /// ImportePagare = 10,000
        /// SaldoPrestamo = 7,000
        /// Resultado = 30 (%)
        /// </summary>
        /// 
        public decimal CalcularImporteLiquidoProyectado(
        decimal importePagare,
        int plazoMeses,
        decimal tasaIntNormal,
        decimal porcenSeguroPasivo,
        decimal porcenFondoGarantia,
        bool esLiquido)
        {
            if (importePagare <= 0)
                return 0m;

            if (esLiquido)
                return Math.Round(importePagare, 2);

            decimal interes = importePagare * (tasaIntNormal / 100m);
            decimal seguro = importePagare * (porcenSeguroPasivo / 100m);
            decimal fondo = importePagare * (porcenFondoGarantia / 100m);

            decimal liquido = importePagare - interes - seguro - fondo;

            return liquido < 0 ? 0m : Math.Round(liquido, 2);
        }

        public decimal CalcularDescuentoProyectado(decimal importePagare, int plazoMeses, decimal tasa)
        {
            if (importePagare <= 0 || plazoMeses <= 0)
                return 0m;

            return Math.Round(importePagare / plazoMeses, 2);
        }
        public decimal CalcularPorcentajeCubierto(decimal importePagare, decimal saldoPrestamo)
        {
            if (importePagare <= 0)
                return 0m;

            if (saldoPrestamo < 0)
                return 100m;

            var porcentaje = 100m - ((saldoPrestamo * 100m) / importePagare);

            if (porcentaje < 0)
                return 0m;

            return Math.Round(porcentaje, 4);
        }

        /// <summary>
        /// Calcula el descuento/amortización a mostrar.
        /// Si el saldo es menor que la amortización, usa el saldo.
        /// </summary>
        public decimal CalcularDescuento(decimal saldoPrestamo, decimal importeAmortizacion)
        {
            if (saldoPrestamo <= 0 || importeAmortizacion <= 0)
                return 0m;

            return saldoPrestamo < importeAmortizacion
                ? saldoPrestamo
                : importeAmortizacion;
        }

        /// <summary>
        /// Calcula el tope de préstamo permitido por ahorros.
        /// Ejemplo: ahorros * 3.5
        /// </summary>
        public decimal CalcularTopePorAhorros(decimal ahorros, decimal factor)
        {
            if (ahorros <= 0 || factor <= 0)
                return 0m;

            return Math.Round(ahorros * factor, 2);
        }

        /* ============================================================
         * AUXILIAR PRIVADO
         * ============================================================ */

        private async Task<decimal> EjecutarFuncionDecimalAsync(string sql, params object[] parametros)
        {
            var resultado = await _context.Set<SqlFuncionResult>()
                .FromSqlRaw(sql, parametros)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return resultado?.Valor ?? 0m;
        }
    }
}
