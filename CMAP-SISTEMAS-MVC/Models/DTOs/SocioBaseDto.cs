namespace CMAP_SISTEMAS_MVC.Models.DTOs
{/// <summary>
 /// DTO con los datos base del socio necesarios
 /// para calcular el estado de cuenta.
 /// </summary>
    public class SocioBaseDto
    {
        public string ClavePension { get; set; } = string.Empty;
        public decimal SaldoAhorros { get; set; }
        public decimal SueldoNetoTotal { get; set; }
        public decimal SaldoPrestamos { get; set; }
        public string? Vigencia { get; set; }
        public string? Situacion { get; set; }
    }
}
