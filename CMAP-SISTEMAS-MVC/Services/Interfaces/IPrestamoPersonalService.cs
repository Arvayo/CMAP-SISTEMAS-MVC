using CMAP_SISTEMAS_MVC.Models.DTOs;

namespace CMAP_SISTEMAS_MVC.Services.Interfaces
{
    public interface IPrestamoPersonalService
    {
        Task<List<EstadoCuentaRowsDto>> GenerarPrestamosPersonalesAsync(
            EstadoCuentaContextDto contexto);
    }
}
