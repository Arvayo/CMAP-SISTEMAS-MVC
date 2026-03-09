using System.ComponentModel.DataAnnotations;
using CMAP_SISTEMAS_MVC.Models.Reportes;
using System.Collections.Generic;

namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class EstadoCuentaIndexVM
    {
        /* ==== Entrada ==== */
        [Required(ErrorMessage = "Ingresa Clave de Pensión.")]
        [StringLength(10, ErrorMessage = "La Pensión debe tener máximo 10 caracteres.")]
        public string Pension { get; set; } = "";

        /* ==== Resultado ==== */
        public string? Mensaje { get; set; }
        public string TipoMensaje { get; set; } = "info";

        public EstadoCuentaReporteVM? Reporte { get; set; }
    }

    public class EstadoCuentaReporteVM
    {
        public string Pension { get; set; } = "";
        public string UsuarioReporte { get; set; } = "";

        public string? NombreCompleto { get; set; }
        public string? SaldoAhorros { get; set; }
        public decimal? SueldoNetoTotal { get; set; }

        public string? Linea3_1 { get; set; }
        public string? Linea3_2 { get; set; }
        public string? Linea4_1 { get; set; }
        public string? Linea4_2 { get; set; }
        public string? Linea5_1 { get; set; }

        public List<RptEdoCuenta> Prestamos { get; set; } = new();
    }
}