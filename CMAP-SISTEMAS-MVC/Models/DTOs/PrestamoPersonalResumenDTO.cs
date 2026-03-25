namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class PrestamoPersonalResumenDTO
    {
        public bool TienePrestamoVigente { get; set; }
        public bool PuedeRenovar { get; set; }
        public bool CumplePago { get; set; }
        public bool CumplePlazo { get; set; }

        public int DiasFaltantes { get; set; }
        public decimal MontoFaltanteParaRenovar { get; set; }

        public decimal SaldoPrestamoActivo { get; set; }
        public decimal LiquidaConPrestamoActivo { get; set; }

        public string MensajeResultado { get; set; } = string.Empty;
    }
}
