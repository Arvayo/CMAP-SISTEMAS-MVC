using CMAP_SISTEMAS_MVC.Models.DTOs;

namespace CMAP_SISTEMAS_MVC.Services.Interfaces
{
    public interface IPrestamoNoPersonalService
    {
        Task<List<EstadoCuentaRowsDto>> GenerarPrestamosNoPersonalesAsync(
            EstadoCuentaContextDto contexto);
    }
}