namespace CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta
{
    public class PrestamoPersonalResumenVM
    {
        public bool TienePrestamoVigente { get; set; }

        public bool PuedeRenovar { get; set; }

        public bool CumplePago { get; set; }

        public bool CumplePlazo { get; set; }

        public int DiasFaltantesParaRenovar { get; set; }

        public decimal MontoFaltanteParaRenovar { get; set; }

        public decimal SaldoPrestamoActivo { get; set; }

        public decimal LiquidaConPrestamoActivo { get; set; }

        public string Mensaje { get; set; } = string.Empty;

        public List<PrestamoPersonalProyeccionVM> Proyecciones { get; set; } = new();
    }
}
