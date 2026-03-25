namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class EstadoCuentaEncabezadoDTO
    {
        public string ClavePension { get; set; } = string.Empty;
        public string NombreSocio { get; set; } = string.Empty;

        public decimal SaldoAhorros { get; set; }
        public decimal AhorroMensual { get; set; }
        public decimal SueldoLiquido { get; set; }

        public decimal SaldoBono { get; set; }
        public decimal ApBonoMensual { get; set; }
        public decimal LiquidezMinima { get; set; }

        public decimal RemanenteIncluidoAhorros { get; set; }
        public decimal AhorrosDiciembreAnterior { get; set; }

        public string ArchivoUbicacion { get; set; } = string.Empty;
        public string NumeroExpediente { get; set; } = string.Empty;
        public string TipoSocio { get; set; } = string.Empty;

        public DateTime FechaSistema { get; set; }
    }
}
