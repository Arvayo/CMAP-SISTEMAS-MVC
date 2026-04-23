namespace CMAP_SISTEMAS_MVC.Services.Interfaces
{
    public interface IPrestamoCalculatorService
    {
        Task<decimal> ObtenerLiquidaConAsync(decimal idPrestamo, DateTime fecha);
        Task<decimal> ObtenerMoratoriosAsync(decimal idPrestamo, DateTime fecha);

        decimal CalcularPorcentajeCubierto(decimal importePagare, decimal saldoPrestamo);
        decimal CalcularDescuento(decimal saldoPrestamo, decimal importeAmortizacion);
        decimal CalcularTopePorAhorros(decimal ahorros, decimal factor);
        decimal CalcularDescuentoProyectado(decimal importePagare, int plazoMeses, decimal tasa);
    }
}
