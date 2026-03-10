namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class PrestamoVigenteDto
    {
        public string? TipoPrestamo { get; set; }
        public decimal SaldoPrestamo { get; set; }
        public DateTime? FechaPrestamo { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public decimal ImportePagare { get; set; }
    }
}
