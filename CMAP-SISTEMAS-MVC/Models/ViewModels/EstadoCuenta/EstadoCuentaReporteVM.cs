using System.Collections.Generic;

namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class EstadoCuentaReporteVM
    {
        public string Pension { get; set; } = "";
        public string UsuarioReporte { get; set; } = "";

        public string? NombreCompleto { get; set; }

        public decimal SaldoAhorros { get; set; }
        public decimal SueldoNetoTotal { get; set; }

        public string? Linea3_1 { get; set; }
        public string? Linea3_2 { get; set; }
        public string? Linea4_1 { get; set; }
        public string? Linea4_2 { get; set; }
        public string? Linea5_1 { get; set; }

        public List<EstadoCuentaRowVM> Prestamos { get; set; } = new();
    }
}
