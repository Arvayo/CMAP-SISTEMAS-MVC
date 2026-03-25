namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class EstadoCuentaContextDto
    {
        /// <summary>
        /// Identificador del reporte o sesión de trabajo.
        /// </summary>
        public string IdReporte { get; set; } = string.Empty;

        /// <summary>
        /// Clave de pensión del socio consultado.
        /// </summary>
        public string ClavePension { get; set; } = string.Empty;

        /// <summary>
        /// Estatus del socio.
        /// Ejemplos:
        /// A = Activo
        /// J = Jubilado
        /// 3 = Empleado / SNTE según reglas heredadas
        /// </summary>
        public string Estatus { get; set; } = string.Empty;

        /// <summary>
        /// Vigencia que se utilizará para consultar los tipos de préstamo.
        /// </summary>
        public string Vigencia { get; set; } = string.Empty;

        /// <summary>
        /// Fecha del sistema usada como base para los cálculos.
        /// Equivale a Fecha_Sistema del código VB.
        /// </summary>
        public DateTime FechaSistema { get; set; }

        /// <summary>
        /// Fecha de ingreso del socio.
        /// Se usa para determinar si pertenece a generación anterior o nueva
        /// en reglas del préstamo personal (PP).
        /// </summary>
        public DateTime? FechaIngreso { get; set; }

        /// <summary>
        /// Saldo de ahorros del socio.
        /// Equivale a MisAhorros.
        /// </summary>
        public decimal MisAhorros { get; set; }

        /// <summary>
        /// Salario del socio utilizado en reglas de alcance.
        /// </summary>
        public decimal Salario { get; set; }

        /// <summary>
        /// Sueldo total del socio.
        /// Equivale a TOTSUELDO.
        /// </summary>
        public decimal TotSueldo { get; set; }

        /// <summary>
        /// Límite de descuento o capacidad máxima de afectación.
        /// Equivale a Ellimite.
        /// </summary>
        public decimal ElLimite { get; set; }

        /// <summary>
        /// Suma o saldo general de préstamos relacionados en reglas de validación.
        /// Equivale a SALDOP.
        /// </summary>
        public decimal SaldoP { get; set; }

        /// <summary>
        /// Meses cotizados del socio.
        /// Equivale a MesesCot.
        /// </summary>
        public int MesesCot { get; set; }

        /// <summary>
        /// Indica si el socio está en un flujo donde ya existe una solicitud activa
        /// o una validación especial que limita nuevos cálculos de alcance.
        /// Equivale a la variable Solicita.
        /// </summary>
        public bool Solicita { get; set; }

        /// <summary>
        /// Indica si se debe trabajar solo con el préstamo GM.
        /// Equivale al comportamiento de Check1 cuando está marcado.
        /// </summary>
        public bool SoloPrestamoGM { get; set; }

        /// <summary>
        /// Indica si ya se generaron descuentos para activos.
        /// Equivale a bDescSec.
        /// </summary>
        public bool DescuentosActivosGenerados { get; set; }

        /// <summary>
        /// Indica si ya se generaron descuentos para jubilados.
        /// Equivale a bDescJub.
        /// </summary>
        public bool DescuentosJubiladosGenerados { get; set; }

        /// <summary>
        /// Indica si ya se generaron descuentos para empleados o SNTE.
        /// Equivale a bDescSnte.
        /// </summary>
        public bool DescuentosSnteGenerados { get; set; }

        /// <summary>
        /// Fecha base de pago para activos.
        /// Equivale a fechaActivo.
        /// </summary>
        public DateTime? FechaActivo { get; set; }

        /// <summary>
        /// Próxima fecha de pago para activos.
        /// Equivale a FecProxActivos.
        /// </summary>
        public DateTime? FecProxActivos { get; set; }

        /// <summary>
        /// Fecha base de pago para jubilados.
        /// Equivale a FechaJub.
        /// </summary>
        public DateTime? FechaJub { get; set; }

        /// <summary>
        /// Próxima fecha de pago para jubilados.
        /// Equivale a FecProxJub.
        /// </summary>
        public DateTime? FecProxJub { get; set; }

        /// <summary>
        /// Fecha base de pago para SNTE o empleados.
        /// Equivale a FechaSnte.
        /// </summary>
        public DateTime? FechaSnte { get; set; }

        /// <summary>
        /// Próxima fecha de pago para SNTE o empleados.
        /// Equivale a FecProxSnte.
        /// </summary>
        public DateTime? FecProxSnte { get; set; }

        /// <summary>
        /// Límite acumulado para préstamo personal basado en ahorros.
        /// Equivale a VECESPP.
        /// </summary>
        public decimal VecesPP { get; set; }

        /// <summary>
        /// Saldo del préstamo personal vigente.
        /// Equivale a SDOPRESTAMOPP.
        /// </summary>
        public decimal SdoPrestamoPP { get; set; }

        /// <summary>
        /// Indica si la operación actual corresponde a una solicitud especial.
        /// Se usa para exceptuar el tope global de 3.5 veces ahorro.
        /// </summary>
        public bool EsSolicitudEspecial { get; set; }

        /// <summary>
        /// Saldo acumulado de los préstamos vigentes que sí participan
        /// en el tope global de 3.5 veces los ahorros.
        /// Ejemplo: PP, PR, RE, ES y PC.
        /// </summary>
        public decimal SaldoPrestamosTopadosAhorro { get; set; }
    }
}

