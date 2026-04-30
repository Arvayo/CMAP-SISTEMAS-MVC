namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class ResultadoPrestamoPersonalDto
    {
        public List<EstadoCuentaRowsDto> FilasProyeccion { get; set; } = new();
        public PrestamoPersonalResumenDTO? Resumen { get; set; }

        public EstadoCuentaRowsDto? FilaPrestamoPPVigente { get; set; }
    }
}
