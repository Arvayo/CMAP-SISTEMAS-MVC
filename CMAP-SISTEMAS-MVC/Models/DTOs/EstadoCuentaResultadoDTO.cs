namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    public class EstadoCuentaResultadoDTO
    {
        public EstadoCuentaEncabezadoDTO Encabezado { get; set; } = new();

        // Sección 2
        public List<EstadoCuentaRowsDto> PrestamosNoPersonales { get; set; } = new();

        // Sección 3: proyección/modalidades PP
        public List<EstadoCuentaRowsDto> PrestamosPersonales { get; set; } = new();

        // Sección 3: resumen narrativo del PP vigente
        public PrestamoPersonalResumenDTO? ResumenPrestamoPersonal { get; set; }
    }
}
