using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class AdminController : Controller
    {

        private readonly ComaviDbContext _context;
        private readonly IConfiguration _configuration;

        public AdminController(ComaviDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private int ExecuteStoredProcedure(string spName, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                var command = new SqlCommand(spName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        [HttpPost]
        public IActionResult RegistrarCamion(Camiones camion)
        {
            var parameters = new[]
            {
                new SqlParameter("@marca", camion.marca),
                new SqlParameter("@modelo", camion.modelo),
                new SqlParameter("@anio", camion.anio),
                new SqlParameter("@numero_placa", camion.numero_placa),
                new SqlParameter("@estado", camion.estado),
                new SqlParameter("@chofer_asignado", camion.chofer_asignado ?? (object)DBNull.Value)
            };
            ExecuteStoredProcedure("sp_RegistrarCamion", parameters);
            return RedirectToAction("Index");
        }

        public IActionResult ActualizarCamion(int id, Camiones camion)
        {
            var parameters = new[]
            {
                new SqlParameter("@id_camion", id),
                new SqlParameter("@marca", camion.marca),
                new SqlParameter("@modelo", camion.modelo),
                new SqlParameter("@anio", camion.anio),
                new SqlParameter("@estado", camion.estado)
            };
            ExecuteStoredProcedure("sp_ActualizarCamion", parameters);
            return RedirectToAction("Index");
        }


        public IActionResult HistorialMantenimiento(int idCamion)
        {
            var historial = _context.Mantenimiento_Camiones
                .FromSqlRaw("EXEC sp_ObtenerHistorialMantenimiento @id_camion", idCamion)
                .ToList();
            return View(historial);
        }

        //TODO: Implementar
        public IActionResult NotificacionesMantenimiento()
        {
            return View();
        }

        [HttpPost]
        public IActionResult DesactivarCamion(int id)
        {
            ExecuteStoredProcedure("sp_DesactivarCamion", new SqlParameter("@id_camion", id));
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult AsignarChofer(int idCamion, int idChofer)
        {
            var parameters = new[]
            {
                new SqlParameter("@id_camion", idCamion),
                new SqlParameter("@id_chofer", idChofer)
            };
            ExecuteStoredProcedure("sp_AsignarChofer", parameters);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult EliminarCamion(int id)
        {
            var result = ExecuteStoredProcedure("sp_EliminarCamion", new SqlParameter("@id_camion", id));
            if (result == 0) TempData["Error"] = "No se puede eliminar por dependencias";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RegistrarChofer(Choferes chofer)
        {
            var parameters = new[]
            {
                new SqlParameter("@nombreCompleto", chofer.nombreCompleto),
                new SqlParameter("@edad", chofer.edad),
                new SqlParameter("@numero_cedula", chofer.numero_cedula),
                new SqlParameter("@licencia", chofer.licencia),
                new SqlParameter("@fecha_venc_licencia", chofer.fecha_venc_licencia),
                new SqlParameter("@estado", chofer.estado),
                new SqlParameter("@genero", chofer.genero)
            };
            ExecuteStoredProcedure("sp_RegistrarChofer", parameters);
            return RedirectToAction("Index");
        }

        //TODO: Implementar
        public IActionResult ActualizarDocumentos()
        {
            return View();
        }

        public IActionResult MonitorearVencimientos(int diasPrevios = 30)
        {
            var documentos = _context.Documentos
                .FromSqlRaw("EXEC sp_MonitorearVencimientos @dias_previos", diasPrevios)
                .ToList();
            return View(documentos);
        }

        [HttpPost]
        public IActionResult ActualizarDatosChofer(int id, Choferes chofer)
        {
            var parameters = new[]
            {
        new SqlParameter("@id_chofer", id),
        new SqlParameter("@nombreCompleto", chofer.nombreCompleto),
        new SqlParameter("@edad", chofer.edad),
        new SqlParameter("@numero_cedula", chofer.numero_cedula),
        new SqlParameter("@licencia", chofer.licencia),
        new SqlParameter("@fecha_venc_licencia", chofer.fecha_venc_licencia),
        new SqlParameter("@genero", chofer.genero)
    };
            ExecuteStoredProcedure("sp_ActualizarDatosChofer", parameters);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DesactivarChofer(int id)
        {
            ExecuteStoredProcedure("sp_DesactivarChofer", new SqlParameter("@id_chofer", id));
            return RedirectToAction("Index");
        }

        //TODO: Implementar
        public IActionResult AsignarDocumentos()
        {
            return View();
        }


        [HttpPost]
        public IActionResult EliminarChofer(int id)
        {
            var result = ExecuteStoredProcedure("sp_EliminarChofer", new SqlParameter("@id_chofer", id));
            if (result == 0) TempData["Error"] = "No se puede eliminar: Chofer tiene registros asociados";
            return RedirectToAction("Index");
        }

    }
}