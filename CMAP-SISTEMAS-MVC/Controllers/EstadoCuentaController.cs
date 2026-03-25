using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta;
using CMAP_SISTEMAS_MVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMAP_SISTEMAS_MVC.Controllers
{
    public class EstadoCuentaController : Controller
    {
        private readonly EstadoCuentaService _estadoCuentaService;

        public EstadoCuentaController(EstadoCuentaService estadoCuentaService)
        {
            _estadoCuentaService = estadoCuentaService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new EstadoCuentaIndexVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(EstadoCuentaIndexVM vm)
        {
            vm.Pension = (vm.Pension ?? "").Trim();

            if (string.IsNullOrWhiteSpace(vm.Pension))
            {
                vm.Mensaje = "Ingresa la Clave de Pensión.";
                vm.TipoMensaje = "error";
                return View(vm);
            }

            try
            {
                var usuario = (User?.Identity?.Name ?? "MVC").Trim();
                if (usuario.Length > 10)
                    usuario = usuario.Substring(0, 10);

                var contexto = new EstadoCuentaContextDto
                {
                    IdReporte = usuario,
                    ClavePension = vm.Pension,
                    Estatus = "A", // temporal, luego lo calculamos correctamente
                    Vigencia = "", // se llenará desde TABLA_DE_SOCIOS
                    FechaSistema = DateTime.Today,
                    MisAhorros = 0,
                    Salario = 0,
                    TotSueldo = 0,
                    ElLimite = 0,
                    SaldoP = 0,
                    MesesCot = 0,
                    Solicita = false,
                    SoloPrestamoGM = false,
                    DescuentosActivosGenerados = false,
                    DescuentosJubiladosGenerados = false,
                    DescuentosSnteGenerados = false
                };

                var resultado = await _estadoCuentaService.GenerarEstadoCuentaAsync(contexto);

                resultado.PrestamosNoPersonales = resultado.PrestamosNoPersonales
                    .Where(x => x.SaldoPrestamo > 0 || x.CantidadPuedeSolicitar > 0)
                    .ToList();

                resultado.PrestamosPersonales = resultado.PrestamosPersonales
                    .Where(x => x.SaldoPrestamo > 0 || x.CantidadPuedeSolicitar > 0)
                    .ToList();

                vm.Resultado = resultado;

                int totalRegistros = vm.Resultado.PrestamosNoPersonales.Count
                                   + vm.Resultado.PrestamosPersonales.Count;

                vm.Mensaje = $"Estado de cuenta generado: {vm.Pension}. Registros encontrados: {totalRegistros}";
                vm.TipoMensaje = "success";
            }
            catch (Exception ex)
            {
                vm.Mensaje = $"Ocurrió un error al generar el estado de cuenta: {ex.Message}";
                vm.TipoMensaje = "error";
            }

            return View(vm);
        }
    }
}