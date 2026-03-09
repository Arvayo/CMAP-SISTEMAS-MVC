/* =========================================================
   SociosController.cs
   - Index GET: muestra formulario
   - Index POST: busca socio por ClavePension
   ========================================================= */

using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace CMAP_SISTEMAS_MVC.Controllers
{
    public class SociosController : Controller
    {
        private readonly Cmap54SistemasContext _db;

        public SociosController(Cmap54SistemasContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new SociosIndexVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SociosIndexVM vm)
        {
            // Normalizar
            vm.ClavePension = (vm.ClavePension ?? "").Trim();
            vm.Nombre = (vm.Nombre ?? "").Trim();
            vm.Apellidos = (vm.Apellidos ?? "").Trim();

            var clave = vm.ClavePension;
            var nombre = vm.Nombre;
            var apellidos = vm.Apellidos;

            // Validación básica
            if (string.IsNullOrWhiteSpace(clave) &&
                string.IsNullOrWhiteSpace(nombre) &&
                string.IsNullOrWhiteSpace(apellidos))
            {
                vm.Socio = null;
                vm.Mensaje = "Ingresa ClavePension o Nombre/Apellidos para buscar.";
                vm.TipoMensaje = "error";
                return View(vm);
            }

            var query = _db.TABLA_DE_SOCIOS.AsNoTracking();

            TablaDeSocio? socioDb = null;

            // 1) Prioridad: ClavePension
            if (!string.IsNullOrWhiteSpace(clave))
            {
                // En SQL Server, comparar CHAR con '=' normalmente ignora espacios a la derecha.
                socioDb = await query.FirstOrDefaultAsync(s => s.ClavePension == clave);
            }
            else
            {
                // 2) Nombre/Apellidos (uno o ambos) - traducible a SQL
                socioDb = await query.FirstOrDefaultAsync(s =>
                    (string.IsNullOrWhiteSpace(nombre) || EF.Functions.Like(s.NombreSocio!, "%" + nombre + "%")) &&
                    (string.IsNullOrWhiteSpace(apellidos) || EF.Functions.Like(s.ApellidosSocio!, "%" + apellidos + "%"))
                );
            }

            // No encontrado
            if (socioDb == null)
            {
                vm.Socio = null;
                vm.Mensaje = "No se encontró ningún socio con esos criterios.";
                vm.TipoMensaje = "error";
                return View(vm);
            }

            // Mapear a VM
            vm.Socio = new SocioResultadoVM
            {
                ClavePension = socioDb.ClavePension?.Trim(),
                NombreSocio = socioDb.NombreSocio?.Trim(),
                ApellidosSocio = socioDb.ApellidosSocio?.Trim(),
                FECHANAC = socioDb.FECHANAC,
                LugarNac = socioDb.LugarNac?.Trim(),
                EdoNac = socioDb.EdoNac?.Trim(),
                SEXO = socioDb.SEXO?.Trim(),
                EDOCIVIL = socioDb.EDOCIVIL?.Trim(),
                
                RFC = socioDb.RFC?.Trim(),
                CURP = socioDb.CURP?.Trim(),
                TelefonoSocio = socioDb.TelefonoSocio?.Trim(),
                TELCELULAR = socioDb.TELCELULAR?.Trim(),
                DireccionSocio = socioDb.DireccionSocio?.Trim(),
                CiudadSocio = socioDb.CiudadSocio?.Trim(),
                COLONIA = socioDb.COLONIA?.Trim(),
                CP = socioDb.CP,
                EMAIL = socioDb.EMAIL?.Trim(),

                CLABE = socioDb.CLABE?.Trim(),
                ClaveSuc = socioDb.ClaveSuc?.Trim(),
                REGION = socioDb.REGION?.Trim(),
                FOLIO = socioDb.FOLIO,
                ARCHIVO = socioDb.ARCHIVO?.Trim(),
                DELEGACION = socioDb.DELEGACION?.Trim(),
                FOLIOCEDULA = socioDb.FOLIOCEDULA,
                BECARIO = socioDb.BECARIO,
                CatSocio = socioDb.CatSocio?.Trim(),
                EstatusActualSocio = socioDb.EstatusActualSocio?.Trim(),
                EmpleadoSindicato = socioDb.EmpleadoSindicato,

                OBS = socioDb.OBS?.Trim(),

                EnvioDesctos = socioDb.EnvioDesctos?.Trim(),
                AhorroPendiente = socioDb.AhorroPendiente,
                TOTSUELDONOMINA = socioDb.TOTSUELDONOMINA,
                SueldoParaDescuentos = socioDb.SueldoParaDescuentos,
                SDOAHORROSINICIAL = socioDb.SDOAHORROSINICIAL,
                SDOPRESTAMOSINI = socioDb.SDOPRESTAMOSINI,
                ULTNOMINA = socioDb.ULTNOMINA,
                ULTAPORTA = socioDb.ULTAPORTA,
                IMPREMANENTE = socioDb.IMPREMANENTE,
                SDOINVER = socioDb.SDOINVER,
                SDOINVERINI = socioDb.SDOINVERINI,
                SaldoAhorros = socioDb.SaldoAhorros,
                SaldoPrestamos = socioDb.SaldoPrestamos,
                SueldoNetoTotal = socioDb.SueldoNetoTotal,


                VIGENCIA = socioDb.VIGENCIA?.Trim(),
                SITUACION = socioDb.SITUACION?.Trim(),
                FechaAltaActivo = socioDb.FechaAltaActivo,
                FechaAltaJubiladoPen = socioDb.FechaAltaJubiladoPen,
                FechaBaja = socioDb.FechaBaja,
                TIPOPENSION = socioDb.TIPOPENSION,
                PENSIONANT = socioDb.PENSIONANT?.Trim(),
                UBICA = socioDb.UBICA?.Trim(),
                REVISO = socioDb.REVISO?.Trim(),
                CT = socioDb.CT,
                FECHACEDULA = socioDb.FECHACEDULA,
                RECCEDULA = socioDb.RECCEDULA?.Trim(),
                FECHACMAP = socioDb.FECHACMAP,
                AVISO = socioDb.AVISO,
                TUTOR = socioDb.TUTOR,
                COTIZANDO = socioDb.COTIZANDO,
                SOLICITA = socioDb.SOLICITA,
                ADSCRIPCION = socioDb.ADSCRIPCION?.Trim(),
                CONTRASENIA = socioDb.CONTRASENIA?.Trim(),
                FECULTAC = socioDb.FECULTAC,
                CONDONACION = socioDb.CONDONACION,
                NIVPRIV = socioDb.NIVPRIV,
                CuentaDebito = socioDb.CuentaDebito?.Trim(),
                AH50 = socioDb.AH50?.Trim(),
                FECHAAH50 = socioDb.FECHAAH50,
                AG = socioDb.AG,

                PorceJub = socioDb.PorceJub,
                RECIBENIVELACION = socioDb.RECIBENIVELACION,
                AportaSindi = socioDb.AportaSindi,
                MontoAportaSindi = socioDb.MontoAportaSindi,
                FechaAportaSindi = socioDb.FechaAportaSindi,
                ACUFORE = socioDb.ACUFORE,
                PORCEFORE = socioDb.PORCEFORE,
                SALDOFORE = socioDb.SALDOFORE,
                SDOFOREINICIAL = socioDb.SDOAHORROSINICIAL,
                SaldoFide = socioDb.SaldoFide,
                SDOFIDEINICIAL = socioDb.SDOAHORROSINICIAL,
                FIDEPROYECTADO = socioDb.FIDEPROYECTADO,
                CTADEP_NIV = socioDb.CTADEP_NIV,
               

            };

            vm.Mensaje = $"{vm.Socio.ClavePension} — {vm.Socio.NombreSocio} {vm.Socio.ApellidosSocio}";
            vm.TipoMensaje = "success";

            return View(vm);
        }
    }
}