using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Models.DTOs
{
    /// <summary>
    /// ============================================================
    /// Modelo Keyless utilizado para recibir resultados de
    /// funciones SQL del sistema.
    ///
    /// Ejemplos:
    ///     dbo.fu_liquidacon(...)
    ///     dbo.fu_calcular_moratorios(...)
    ///
    /// EF Core necesita una proyección para mapear el resultado
    /// de funciones escalares.
    /// ============================================================
    /// </summary>
    [Keyless]
    public class SqlFuncionResult
    {
        public decimal Valor { get; set; }
    }
}
