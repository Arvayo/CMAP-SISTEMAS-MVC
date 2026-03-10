namespace CMAP_SISTEMAS_MVC.Models
{
    public class TablaDeTiposDePrestamo
    {
        public string? ClavePrestamo { get; set; }

        public string? NombrePrestamo { get; set; }

        public string? ClaveRenovacion { get; set; }

        public decimal? PorcenRenova { get; set; }

        public string? Esliquido { get; set; }

        public decimal? PlazoRenovar { get; set; }

        public decimal? VecesAhorro { get; set; }

        public string? ClaveDesctoSec { get; set; }
    }
}
