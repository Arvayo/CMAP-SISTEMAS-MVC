using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    [Keyless]
    public class PrestamoVigenteDto
    {
        public decimal Id { get; set; }
        public string? TipoPrestamo { get; set; } =  string.Empty;
        public int? SubCve { get; set; }

        public decimal SaldoPrestamo { get; set; }
        public decimal ImportePagare { get; set; }
        public decimal ImporteAmortizacion { get; set; }

        public byte NumMesesPrestamo { get; set; }
        public decimal NumeroPagare { get; set; }

        public DateTime? FechaPrestamo { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public decimal LiquidaCon { get; set; } // viene de dbo.fu_liquidacon
    }
}
