namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    /// <summary>
    /// DTO que representa la configuración de un tipo de préstamo
    /// que puede ser utilizado en el cálculo del estado de cuenta.
    /// 
    /// Esta información proviene de las tablas:
    /// 
    /// TABLA_DE_TIPOS_DE_PRESTAMOS
    /// DETALLE_DE_TIPOS_DE_PRESTAMOS
    /// 
    /// y se utiliza para determinar:
    /// 
    /// - reglas de renovación
    /// - tasas de interés
    /// - montos máximos
    /// - número de meses
    /// - alcance basado en ahorros
    /// </summary>
    public class TipoPrestamoDto
    {
        /// <summary>
        /// Clave del préstamo.
        /// Ejemplos: PP, ES, EX, GM, VA, PC, etc.
        /// </summary>
        public string ClavePrestamo { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo del préstamo.
        /// </summary>
        public string NombrePrestamo { get; set; } = string.Empty;

        /// <summary>
        /// Subclave del préstamo, si aplica.
        /// </summary>
        public int? SubClave { get; set; }

        /// <summary>
        /// Indica si el préstamo está vigente.
        /// </summary>
        public string? Vigente { get; set; }

        /// <summary>
        /// Plazo máximo del préstamo.
        /// </summary>
        public int PlazoMaximo { get; set; }

        /// <summary>
        /// Tasa de interés normal.
        /// </summary>
        public decimal TasaIntNormal { get; set; }

        /// <summary>
        /// Monto máximo del préstamo.
        /// </summary>
        public decimal MontoMaximo { get; set; }

        /// <summary>
        /// Veces ahorro para cálculo de alcance.
        /// </summary>
        public decimal VecesAhorro { get; set; }

        /// <summary>
        /// Porcentaje requerido para renovación.
        /// </summary>
        public decimal PorcenRenova { get; set; }

        /// <summary>
        /// Indica si el préstamo es líquido.
        /// </summary>
        public string? EsLiquido { get; set; }

        /// <summary>
        /// Porcentaje de seguro pasivo.
        /// </summary>
        public decimal PorcenSeguroPasivo { get; set; }

        /// <summary>
        /// Porcentaje de fondo de garantía.
        /// </summary>
        public decimal PorcenFondoGarantia { get; set; }

        /// <summary>
        /// Indicador de renovación del préstamo.
        /// En tu entidad actual viene como string.
        /// </summary>
        public string? ClaveRenovacion { get; set; }

        /// <summary>
        /// Porcentaje del plazo requerido para renovar.
        /// </summary>
        public decimal PlazoRenovar { get; set; }

        /// <summary>
        /// Factor sobre ahorro configurado en el detalle del préstamo.
        /// </summary>
        public decimal FactorSobreAhorro { get; set; }

        /// <summary>
        /// Meses mínimos cotizados requeridos.
        /// </summary>
        public int MesesMinCotizados { get; set; }
    }
}
