namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class EstadoCuentaRowVM
    {
        public string TipoPrestamo { get; set; } = string.Empty;
        public string NombrePrestamo { get; set; } = string.Empty;
        public string ClavePension { get; set; } = string.Empty;
        public string? Subcve { get; set; }

        public DateTime? FechaPrestamo { get; set; }
        public decimal ImportePrestamo { get; set; }
        public int PlazoMeses { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public decimal Saldo { get; set; }
        public decimal CantidadPuedeSolicitar { get; set; }
        public decimal ImporteLiquido { get; set; }
        public decimal Descuentos { get; set; }
        public decimal LiquidaCon { get; set; }

        public bool TienePrestamoVigente => Saldo > 0;
        public bool PuedeSolicitar => CantidadPuedeSolicitar > 0;
        public bool DebeMostrarse => TienePrestamoVigente || PuedeSolicitar;
    }
}
