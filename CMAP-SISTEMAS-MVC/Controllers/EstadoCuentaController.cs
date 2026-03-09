using CMAP_SISTEMAS_MVC.Data;
using CMAP_SISTEMAS_MVC.Models.ViewModels.EstadoCuenta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CMAP_SISTEMAS_MVC.Controllers
{
    public class EstadoCuentaController : Controller
    {
        private readonly Cmap54SistemasContext _db;

        public EstadoCuentaController(Cmap54SistemasContext db)
        {
            _db = db;
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

            // Usuario “dueño” del reporte
            var usuario = (User?.Identity?.Name ?? "MVC").Trim();
            if (usuario.Length > 10) usuario = usuario.Substring(0, 10);

            /* === 1) Limpieza previa === 
            await _db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dbo.rptedocuenta WHERE IdReporte = {0}", usuario);
            */
            /* === 2) Ejecutar SP === 

            var dbName = _db.Database.GetDbConnection().Database;
            var server = _db.Database.GetDbConnection().DataSource;

            return Content($"DB={dbName} | Server={server}");
            */

            var pPension  = new SqlParameter("@PENSION", SqlDbType.Char, 10) { Value = vm.Pension };
            var pUsuario = new SqlParameter("@USUARIO", SqlDbType.Char, 10) { Value = usuario };

            var pLimite = new SqlParameter("@LIMITE", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pMesesCot = new SqlParameter("@MESESCOT", SqlDbType.Int) { Value = 0 };
            var pEstatus = new SqlParameter("@ESTATUS", SqlDbType.Char, 1) { Value = "A" };

            var pSueldo = new SqlParameter("@SUELDO", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pSaldoAho = new SqlParameter("@SALDOAHORROS", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pSaldoPre = new SqlParameter("@SALDOPRESTAMOS", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pSaldoP = new SqlParameter("@SALDOP", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };

            var pNomSoc = new SqlParameter("@NOMBRESOCIO", SqlDbType.VarChar, 60) { Value = "" };
            var pApeSoc = new SqlParameter("@APELLIDOSSOCIO", SqlDbType.VarChar, 60) { Value = "" };

            var pSueldoNP = new SqlParameter("@SUELDONETOPRINT", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pSolicita = new SqlParameter("@SOLICITA", SqlDbType.Char, 1) { Value = "S" };

            var pSaldoFor = new SqlParameter("@SALDOFORE", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };
            var pSaldoFid = new SqlParameter("@SALDOFIDE", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = 0m };

            // 👇 ESTE es el que casi siempre truena si no va tipado:
            var pUltNom = new SqlParameter("@ULTNOMINA", SqlDbType.SmallDateTime) { Value = DBNull.Value };

            var pCate = new SqlParameter("@CATEGO", SqlDbType.VarChar, 20) { Value = "" };
            var pFolio = new SqlParameter("@FOLIO", SqlDbType.BigInt) { Value = 0L };
            var pVig = new SqlParameter("@VIGENCIA", SqlDbType.VarChar, 20) { Value = "" };

            // (Opcional) para no reventar a los 30s mientras arreglamos el SP
            _db.Database.SetCommandTimeout(300); // 5 minutos


            await _db.Database.ExecuteSqlRawAsync("SELECT 1"); // ping rápido

            await _db.Database.ExecuteSqlRawAsync(@"        
EXEC dbo.SP_ESTADO_DE_CUENTA
    @PENSION,
    @USUARIO,
    @LIMITE,
    @MESESCOT,
    @ESTATUS,
    @SUELDO,
    @SALDOAHORROS,
    @SALDOPRESTAMOS,
    @SALDOP,
    @NOMBRESOCIO,
    @APELLIDOSSOCIO,
    @SUELDONETOPRINT,
    @SOLICITA,
    @SALDOFORE,
    @SALDOFIDE,
    @ULTNOMINA,
    @CATEGO,
    @FOLIO,
    @VIGENCIA
",
            pPension,
            pUsuario,
            pLimite,
            pMesesCot,
            pEstatus,
            pSueldo,
            pSaldoAho,
            pSaldoPre,
            pSaldoP,
            pNomSoc,
            pApeSoc,
            pSueldoNP,
            pSolicita,
            pSaldoFor,
            pSaldoFid,
            pUltNom,
            pCate,
            pFolio,
            pVig
            
 );
            /* === 3) Leer tabla rptedocuenta === */
            var rows = await _db.RptEdoCuenta
                .AsNoTracking()
                .Where(r => r.IdReporte == usuario && r.ClavePension == vm.Pension)
                .OrderBy(r => r.TipoPrestamo)
                .ThenBy(r => r.FechaPrestamo)
                .ToListAsync();

            if (rows.Count == 0)
            {
                vm.Mensaje = "No se generó información en rptedocuenta. Verifica que el SP esté llenando datos para esa pensión.";
                vm.TipoMensaje = "error";
                return View(vm);
            }

            var first = rows.First();

            vm.Reporte = new EstadoCuentaReporteVM
            {
                Pension = vm.Pension,
                UsuarioReporte = usuario,
                NombreCompleto = $"{first.Nombresocio} {first.Apellidossocio}".Trim(),
                SaldoAhorros = first.Saldoahorros,
                SueldoNetoTotal = first.Sueldonetototal,
                Linea3_1 = first.Tercer_linea_1,
                Linea3_2 = first.Tercer_linea_2,
                Linea4_1 = first.Cuarta_linea_1,
                Linea4_2 = first.Cuarta_linea_2,
                Linea5_1 = first.Quinta_linea_1,
                Prestamos = rows
            };

            vm.Mensaje = $"Estado de cuenta generado: {vm.Pension}";
            vm.TipoMensaje = "success";

            return View("Reporte", vm);
        }
    }
}

