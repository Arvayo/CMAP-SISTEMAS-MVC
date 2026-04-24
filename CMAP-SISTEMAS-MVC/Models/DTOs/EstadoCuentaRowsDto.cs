namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class EstadoCuentaRowsDto
    {
        /// <summary>
        /// Identificador del reporte o sesión de consulta.
        /// </summary>
        public string IdReporte { get; set; } = string.Empty;

        /// <summary>
        /// Clave de pensión del socio consultado.
        /// </summary>
        public string ClavePension { get; set; } = string.Empty;

        /// <summary>
        /// Clave del tipo de préstamo.
        /// Ejemplo: PP, ES, EX, GM, VA, etc.
        /// </summary>
        public string ClavePrestamo { get; set; } = string.Empty;  // interna

        /// <summary>
        /// Subclave del préstamo, si aplica.
        /// </summary>
        public int? SubClave { get; set; }                         // interna

        /// <summary>
        /// Nombre descriptivo del préstamo.
        /// </summary>
        public string NombrePrestamo { get; set; } = string.Empty;

        /// <summary>
        /// Fecha en la que fue otorgado el préstamo vigente.
        /// Si no existe préstamo vigente para ese tipo, puede quedar nula.
        /// </summary>
        public DateTime? FechaPrestamo { get; set; }

        /// <summary>
        /// Importe original del préstamo o pagaré.
        /// </summary>
        public decimal ImportePrestamo { get; set; }

        /// <summary>
        /// Plazo del préstamo en meses.
        /// </summary>
        public int PlazoMeses { get; set; }

        /// <summary>
        /// Fecha de vencimiento del préstamo vigente.
        /// </summary>
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>
        /// Saldo actual pendiente del préstamo.
        /// </summary>
        public decimal SaldoPrestamo { get; set; }

        /// <summary>
        /// Cantidad que el socio puede solicitar según las reglas de negocio.
        /// </summary>
        public decimal CantidadPuedeSolicitar { get; set; }

        /// <summary>
        /// Importe líquido estimado que recibiría el socio después de deducciones.
        /// </summary>
        public decimal ImporteLiquido { get; set; }

        /// <summary>
        /// Descuento o amortización estimada por periodo.
        /// </summary>
        public decimal? Descuento { get; set; }

        /// <summary>
        /// Monto estimado para liquidar o renovar el préstamo actual.
        /// </summary>
        public decimal? LiquidaCon { get; set; }

        /// <summary>
        /// Indica si actualmente existe un préstamo vigente para este tipo/subclave.
        /// </summary>
        public bool TienePrestamoVigente { get; set; }

        /// <summary>
        /// Estatus resumido de la fila.
        /// Ejemplos: VIGENTE, DISPONIBLE, SIN_ALCANCE.
        /// </summary>
        public string EstatusFila { get; set; } = string.Empty;

        /// <summary>
        /// Texto auxiliar u observaciones de negocio para mostrar en la fila.
        /// </summary>
        public string Observaciones { get; set; } = string.Empty;

        /// <summary>
        /// Orden de despliegue de la fila en el estado de cuenta.
        /// </summary>
        public int Orden { get; set; }

        /// <summary>
        /// Orden para mostrar en la tabla (como en VB).
        /// </summary>
        public int OrdenVisual { get; set; }

        /// <summary>
        /// Importe líquido proyectado (cuando aplica).
        /// </summary>
       
        /// <summary>
        /// Indica si la fila es una proyección (no existe préstamo real).
        /// </summary>
        public bool EsProyeccion { get; set; }

        /// <summary>
        /// Indica si el préstamo está vigente.
        /// </summary>
        public bool EstaVigente { get; set; }

        public decimal TasaInteres { get; set; }
    }
}