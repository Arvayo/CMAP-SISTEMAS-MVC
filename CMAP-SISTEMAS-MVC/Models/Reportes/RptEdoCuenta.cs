using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;


namespace CMAP_SISTEMAS_MVC.Models.Reportes
{
    [Keyless]
    [Table("rptedocuenta")]
    public class RptEdoCuenta
    {
        public string? IdReporte { get; set; }
        public string? TipoPrestamo { get; set; }
        public string? ClavePension { get; set; }
        public string? NombrePrestamo { get; set; }

        public byte? CLAVERENOVACION { get; set; }
       
        public decimal? PORCENRENOVA { get; set; }
        public DateTime? FechaPrestamo { get; set; }
        public decimal? ImportePrestamo { get; set; }
        public short? PlazoMeses { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public decimal? Saldo { get; set; }
        public decimal? CantidadPuedeSolicitar { get; set; }
        public decimal? ImporteLiquido { get; set; }
        public decimal? Descuentos { get; set; }

        public int? Subcve { get; set; }
        public string? Nombresocio { get; set; }
        public string? Apellidossocio { get; set; }

        public string? Saldoahorros { get; set; }
        public decimal? Sueldonetototal { get; set; }

        public long? Id { get; set; }

        public string? Ahorroper { get; set; }
        public string? Tercer_linea_1 { get; set; }
        public string? Tercer_linea_2 { get; set; }
        public string? Cuarta_linea_1 { get; set; }
        public string? Cuarta_linea_2 { get; set; }
        public string? Quinta_linea_1 { get; set; }

        public string? Catsocio { get; set; }
        public long? Folio { get; set; }

        public decimal? LiquidaCon { get; set; }
    }
}
    
