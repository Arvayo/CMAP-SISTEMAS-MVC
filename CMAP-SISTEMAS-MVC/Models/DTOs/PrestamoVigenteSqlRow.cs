using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    /// <summary>
    /// ============================================================
    /// Proyección SQL Keyless
    /// ------------------------------------------------------------
    /// Esta clase NO representa una tabla física.
    /// 
    /// Se utiliza para mapear resultados de consultas SQL que
    /// incluyen columnas calculadas provenientes de funciones
    /// SQL Server como:
    /// 
    ///     dbo.fu_liquidacon(...)
    /// 
    /// EF Core no puede mapear estas columnas a entidades
    /// normales, por lo que se usa un modelo Keyless.
    /// ============================================================
    /// </summary>
    [Keyless]
    public class PrestamoVigenteSqlRow
    {
        /// <summary>
        /// Identificador del préstamo.
        /// </summary>
        public decimal Id { get; set; }

        /// <summary>
        /// Tipo de préstamo (PP, ES, EX, etc.).
        /// </summary>
        public string? TipoPrestamo { get; set; } = string.Empty;

        /// <summary>
        /// Subclave utilizada en algunos préstamos
        /// (ej. prendarios o varios).
        /// </summary>
        public int? SubCve { get; set; }

        /// <summary>
        /// Saldo actual del préstamo.
        /// </summary>
        public decimal SaldoPrestamo { get; set; }

        /// <summary>
        /// Importe original del pagaré.
        /// </summary>
        public decimal ImportePagare { get; set; }

        /// <summary>
        /// Importe de amortización mensual.
        /// </summary>
        public decimal ImporteAmortizacion { get; set; }

        /// <summary>
        /// Número de meses del préstamo.
        /// </summary>
        public byte NumMesesPrestamo { get; set; }

        /// <summary>
        /// Número de pagaré.
        /// </summary>
        public decimal NumeroPagare { get; set; }

        /// <summary>
        /// Fecha en que se otorgó el préstamo.
        /// </summary>
        public DateTime? FechaPrestamo { get; set; }

        /// <summary>
        /// Fecha de vencimiento del préstamo.
        /// </summary>
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>
        /// Importe necesario para liquidar el préstamo
        /// calculado mediante dbo.fu_liquidacon().
        /// </summary>
        public decimal LiquidaCon { get; set; }
    }
}