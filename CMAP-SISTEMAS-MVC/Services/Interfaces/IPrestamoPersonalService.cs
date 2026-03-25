using CMAP_SISTEMAS_MVC.Models.DTOs;

namespace CMAP_SISTEMAS_MVC.Services.Interfaces
{
    public interface IPrestamoPersonalService
    {
        Task<ResultadoPrestamoPersonalDto> GenerarPrestamosPersonalesAsync(EstadoCuentaContextDto contexto);
    }
}
