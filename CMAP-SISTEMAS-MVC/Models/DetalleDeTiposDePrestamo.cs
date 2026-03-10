namespace CMAP_SISTEMAS_MVC.Models
{
    public class DetalleDeTiposDePrestamo
    {
        public string? ClavePrestamo { get; set; }

        public string? TipoSocio { get; set; }

        public string? Vigencia { get; set; }

        public short? PlazoMaximo { get; set; }

        public decimal? TasaIntNormal { get; set; }

        public decimal? MontoMaximo { get; set; }

        public decimal? PorcenSeguroPasivo { get; set; }

        public decimal? PorcenFondoGarantia { get; set; }

        public decimal? FactorSobreAhorro { get; set; }

        public short? MesesMinCotizados { get; set; }

        public string? Vigente { get; set; }

        public int? Subcve { get; set; }

        public int NombrePrestamo { get; set; }
    }
}
