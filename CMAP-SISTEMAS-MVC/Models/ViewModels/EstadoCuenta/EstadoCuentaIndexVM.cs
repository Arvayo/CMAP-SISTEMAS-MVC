using CMAP_SISTEMAS_MVC.Models.DTOs;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class EstadoCuentaIndexVM
    {/* ===============================
         * Entrada del formulario
         * =============================== */
        [Required(ErrorMessage = "Ingresa Clave de Pensión.")]
        [StringLength(10, ErrorMessage = "La Pensión debe tener máximo 10 caracteres.")]
        public string Pension { get; set; } = "";

        /* ===============================
         * Mensajes de la pantalla
         * =============================== */
        public string? Mensaje { get; set; }

        // success | error | info
        public string TipoMensaje { get; set; } = "info";

        /* ===============================
         * Resultado del reporte
         * =============================== */
        public List<EstadoCuentaRowsDto> Reporte { get; set; } = new();
    }

   
}