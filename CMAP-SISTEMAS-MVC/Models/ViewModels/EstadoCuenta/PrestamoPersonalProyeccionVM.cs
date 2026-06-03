namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class PrestamoPersonalProyeccionVM
    {
        public string NombrePrestamo { get; set; } = string.Empty;

        public decimal PuedeSolicitar { get; set; }

        public decimal ImporteLiquido { get; set; }

        public decimal Descuento { get; set; }

        public int PlazoMeses { get; set; }

        public int NumeroPagos { get; set; }

        public decimal TasaInteres { get; set; }

        public decimal PorcentajeRenovacion { get; set; }

        public decimal PlazoRenovacion { get; set; }
    }
}
