using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMAP_SISTEMAS_MVC.Models
{
    [Keyless] // ✅ Para poder consultar aunque la tabla no tenga PK
    [Table("TABLA_DE_SOCIOS")]
    public class TablaDeSocio
    {
        public string? ClavePension { get; set; }
        public string? NombreSocio { get; set; }
        public string? ApellidosSocio { get; set; }
        public DateTime? FECHANAC { get; set; }
        public string? LugarNac { get; set; }
        public string? EdoNac { get; set; }
        public string? SEXO { get; set; }
        public string? EDOCIVIL { get; set; }
        public string? RFC { get; set; }
        public string? CURP { get; set; }
        
        public string? TelefonoSocio { get; set; }
        public string? TELCELULAR { get; set; }
        public string? DireccionSocio { get; set; }
        public string? OBS { get; set; }


        public string? CLABE { get; set; }
        public string? ClaveSuc { get; set; }
        public string? REGION { get; set; }
        public long? FOLIO { get; set; }
        public string? ARCHIVO { get; set; }
        public string? DELEGACION { get; set; }
        public long? FOLIOCEDULA { get; set; }
        public byte? BECARIO { get; set; }
        public string? CatSocio { get; set; }
        public string? EstatusActualSocio { get; set; }
        public string? EmpleadoSindicato { get; set; }

        
        public string? EnvioDesctos { get; set; }
        public decimal? AhorroPendiente { get; set; }
        public decimal? TOTSUELDONOMINA { get; set; }
        public decimal? SueldoParaDescuentos { get; set; }
        public decimal? SDOAHORROSINICIAL { get; set; }
        public decimal? SDOPRESTAMOSINI { get; set; }
        public DateTime? ULTNOMINA { get; set; }
        public decimal? ULTAPORTA { get; set; }
        public decimal? IMPREMANENTE { get; set; }
        public decimal? SDOINVER { get; set; }
        public decimal? SDOINVERINI { get; set; }
        

        public string? CiudadSocio { get; set; }
        public string? COLONIA { get; set; }
        public int? CP { get; set; }
        public string? EMAIL { get; set; }
        public decimal? SaldoAhorros { get; set; }
        public decimal? SaldoPrestamos { get; set; }
        public decimal? SueldoNetoTotal { get; set; }

        // Agrega solo las columnas que vayas a usar hoy.
        // Luego ya la completamos o metemos Scaffold.

        public string? VIGENCIA { get; set; }
        public string? SITUACION { get; set; }
        public DateTime? FechaAltaActivo { get; set; }
        public DateTime? FechaAltaJubiladoPen { get; set; }
        public DateTime? FechaBaja { get; set; }
        public byte? TIPOPENSION { get; set; }
        public string? PENSIONANT { get; set; }
        public string? UBICA { get; set; }
        public string? REVISO { get; set; }
        public byte? CT { get; set; }
        public DateTime? FECHACEDULA { get; set; }
        public string? RECCEDULA { get; set; }
        public DateTime? FECHACMAP { get; set; }
        public byte? AVISO { get; set; }
        public byte? TUTOR { get; set; }
        public byte? COTIZANDO { get; set; }
        public short? SOLICITA { get; set; }
        public string? ADSCRIPCION { get; set; }
        public string? CONTRASENIA { get; set; }
        public DateTime? FECULTAC { get; set; }
        public byte? CONDONACION { get; set; }
        public short? NIVPRIV { get; set; }
        public string? CuentaDebito { get; set; }
        public string? AH50 { get; set; }
        public DateTime? FECHAAH50 { get; set; }
        public byte? AG { get; set; }

        public decimal? PorceJub { get; set; }
        public bool? RECIBENIVELACION { get; set; }
        public byte? AportaSindi { get; set; }
        public decimal? MontoAportaSindi { get; set; }
        public DateTime? FechaAportaSindi { get; set; }
        public byte? ACUFORE { get; set; }
        public decimal? PORCEFORE { get; set; }
        public decimal? SALDOFORE { get; set; }
        public decimal? SDOFOREINICIAL { get; set; }
        public decimal? SaldoFide { get; set; }
        public decimal? SDOFIDEINICIAL { get; set; }
        public decimal? FIDEPROYECTADO { get; set; }
        public string? CTADEP_NIV { get; set; }
    }
}
