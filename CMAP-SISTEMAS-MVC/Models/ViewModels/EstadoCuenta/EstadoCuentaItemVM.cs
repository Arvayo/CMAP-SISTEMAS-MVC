namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class EstadoCuentaItemVM
    {
        public string TipoPrestamo { get; set; } = string.Empty;
        public string NombrePrestamo { get; set; } = string.Empty;
        public string ClavePension { get; set; } = string.Empty;

        public DateTime? FechaPrestamo { get; set; }
        public decimal ImportePrestamo { get; set; }
        public int PlazoMeses { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public decimal Saldo { get; set; }
        public decimal CantidadPuedeSolicitar { get; set; }
        public decimal ImporteLiquido { get; set; }
        public decimal Descuentos { get; set; }

        public decimal LiquidaCon { get; set; }

        public string? Subcve { get; set; }

        public bool TienePrestamoVigente { get; set; }
        public bool PuedeCalcularse { get; set; }
    }
}
