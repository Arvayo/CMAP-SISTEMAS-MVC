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

        /// <summary>
        /// Fecha de ingreso del socio.
        /// Se usa para determinar generación (PP 2.41 / 1.85)
        /// </summary>
        public DateTime? FechaIngreso { get; set; }

        /// <summary>
        /// Estatus del socio (A, J, 3, etc.)
        /// </summary>
        public string Estatus { get; set; } = string.Empty;

        /// <summary>
        /// Salario base del socio (para reglas de descuento)
        /// </summary>
        public decimal Salario { get; set; }

        /// <summary>
        /// Límite de descuento permitido (Ellimite en VB)
        /// </summary>
        public decimal LimiteDescuento { get; set; }
    }
}
