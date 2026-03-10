namespace CMAP_SISTEMAS_MVC.Models
{
    public class TablaDePrestamo
    {
        public decimal? Id { get; set; }

        public string? ClavePension { get; set; }

        public string? TipoPrestamo { get; set; }

        public decimal? ImportePagare { get; set; }

        public decimal? SaldoPrestamo { get; set; }

        public decimal? ImporteAmortizacion { get; set; }

        public byte? NumMesesPrestamo { get; set; }

        public DateTime? FechaPrestamo { get; set; }

        public DateTime? FechaVencimiento { get; set; }

        public decimal? NumeroPagare { get; set; }

        public string? EstatusPrestamo { get; set; }

        /*
        public decimal? IntMorA { get; set; }
        */
    }
}
