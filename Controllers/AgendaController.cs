using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
#nullable disable
#pragma warning disable CS0168

    [Authorize(Roles = "admin,user")]
    public class AgendaController : Controller
    {
        private readonly ComaviDbContext _context;

        public AgendaController(
            ComaviDbContext context)
        {
            _context = context;
        }

        // Reemplaza los métodos existentes con estas versiones actualizadas

        public async Task<IActionResult> Index()
        {
            try
            {
                // Verificar si el usuario está autenticado
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Index", "Login");
                }

                // Obtener el ID del usuario con validación
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return RedirectToAction("Index", "Login", new { returnUrl = "/Agenda/Index" });
                }

                int userId = int.Parse(userIdClaim);

                var eventos = await _context.EventosAgenda
                    .Include(e => e.Chofer)
                    .Where(e => e.id_usuario == userId)
                    .OrderBy(e => e.fecha_inicio)
                    .ToListAsync();

                return View(eventos);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al cargar los eventos. Por favor, inténtelo de nuevo.";
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> Calendar()
        {
            try
            {
                // Verificar si el usuario está autenticado
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Index", "Login");
                }

                // Obtener el ID del usuario con validación
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return RedirectToAction("Index", "Login", new { returnUrl = "/Agenda/Calendar" });
                }

                int userId = int.Parse(userIdClaim);

#pragma warning disable CS8601 // Possible null reference assignment.
                var eventos = await _context.EventosAgenda
                    .Where(e => e.id_usuario == userId)
                    .Select(e => new CalendarEvent
                    {
                        id = e.id_evento.ToString(),
                        title = e.titulo,
                        start = e.fecha_inicio.ToString("yyyy-MM-dd HH:mm:ss"),
                        end = e.fecha_fin != null ? e.fecha_fin.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                        className = GetEventClass(e.tipo_evento, e.estado),
                        description = e.descripcion
                    })
                    .ToListAsync();
#pragma warning restore CS8601 // Possible null reference assignment.

                ViewBag.Events = System.Text.Json.JsonSerializer.Serialize(eventos);
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al cargar el calendario. Por favor, inténtelo de nuevo.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                // Verificar si el usuario está autenticado
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Index", "Login");
                }

                // Obtener el ID del usuario con validación
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return RedirectToAction("Index", "Login", new { returnUrl = "/Agenda/Create" });
                }

                int userId = int.Parse(userIdClaim);

                // Obtener choferes asociados al usuario actual
                var choferes = _context.Choferes
                    .Where(c => c.id_usuario == userId || c.id_usuario == null)
                    .OrderBy(c => c.nombreCompleto)
                    .ToList();

                // Poblar listas desplegables
                ViewBag.Choferes = new SelectList(choferes, "id_chofer", "nombreCompleto");

                // Inicializar con fecha actual para ayudar a prevenir errores
                var nuevoEvento = new EventoAgenda
                {
                    fecha_inicio = new DateTime(DateTime.Now.AddHours(1).Year,
                               DateTime.Now.AddHours(1).Month,
                               DateTime.Now.AddHours(1).Day,
                               DateTime.Now.AddHours(1).Hour,
                               DateTime.Now.AddHours(1).Minute,
                               0),
                    requiere_notificacion = false,
                    dias_anticipacion_notificacion = 1,
                    estado = "Pendiente"
                };

                return View(nuevoEvento);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al cargar el formulario. Por favor, inténtelo de nuevo.";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventoAgenda eventoAgenda)
        {
            try
            {
                // Eliminar errores de ModelState para las propiedades de navegación
                ModelState.Remove("Usuario");
                ModelState.Remove("Chofer");

                // Verificar si el usuario está autenticado
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Index", "Login");
                }

                // Obtener el ID del usuario con validación
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    ModelState.AddModelError(string.Empty, "Error de autenticación. Por favor, inicie sesión nuevamente.");
                    ViewBag.Choferes = new SelectList(_context.Choferes, "id_chofer", "nombreCompleto");
                    return View(eventoAgenda);
                }

                // Validaciones adicionales
                if (eventoAgenda.fecha_inicio < DateTime.Now.Date)
                {
                    ModelState.AddModelError("fecha_inicio", "La fecha de inicio no puede ser anterior a la fecha actual.");
                }

                if (eventoAgenda.fecha_fin.HasValue && eventoAgenda.fecha_fin <= eventoAgenda.fecha_inicio)
                {
                    ModelState.AddModelError("fecha_fin", "La fecha de fin debe ser posterior a la fecha de inicio.");
                }

                // Verificar si el modelo es válido
                if (!ModelState.IsValid)
                {
                    // Volver a cargar las listas desplegables
                    ViewBag.Choferes = new SelectList(_context.Choferes, "id_chofer", "nombreCompleto", eventoAgenda.id_chofer);
                    return View(eventoAgenda);
                }

                // Asignar usuario
                int userId = int.Parse(userIdClaim);
                eventoAgenda.id_usuario = userId;

                // Validación de relación entre chofer y usuario (si es necesario)
                if (eventoAgenda.id_chofer.HasValue)
                {
                    var chofer = await _context.Choferes.FindAsync(eventoAgenda.id_chofer.Value);
                    if (chofer == null)
                    {
                        ModelState.AddModelError("id_chofer", "El chofer seleccionado no existe.");
                        ViewBag.Choferes = new SelectList(_context.Choferes, "id_chofer", "nombreCompleto", eventoAgenda.id_chofer);
                        return View(eventoAgenda);
                    }
                }


                // Configurar valores predeterminados si es necesario
                if (eventoAgenda.requiere_notificacion && !eventoAgenda.dias_anticipacion_notificacion.HasValue)
                {
                    eventoAgenda.dias_anticipacion_notificacion = 3; // Valor predeterminado
                }

                // Añadir a la base de datos
                _context.Add(eventoAgenda);

                // Guardar cambios y registrar resultado
                int result = await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {

                // Añadir error al ModelState para mostrarlo al usuario
                ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar el evento. Por favor, inténtelo de nuevo.");

                // Volver a cargar las listas desplegables
                ViewBag.Choferes = new SelectList(_context.Choferes, "id_chofer", "nombreCompleto", eventoAgenda.id_chofer);
                return View(eventoAgenda);
            }
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Verificar si el usuario está autenticado
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Login");
            }

            // Obtener el ID del usuario con validación
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Index", "Login", new { returnUrl = $"/Agenda/Edit/{id}" });
            }

            int userId = int.Parse(userIdClaim);

            var eventoAgenda = await _context.EventosAgenda
                .FirstOrDefaultAsync(e => e.id_evento == id && e.id_usuario == userId);

            if (eventoAgenda == null)
            {
                return NotFound();
            }

            // Obtener choferes asociados al usuario actual
            var choferes = _context.Choferes
                .Where(c => c.id_usuario == userId || c.id_usuario == null)
                .OrderBy(c => c.nombreCompleto)
                .ToList();

            ViewBag.Choferes = new SelectList(choferes, "id_chofer", "nombreCompleto", eventoAgenda.id_chofer);

            return View(eventoAgenda);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EventoAgenda eventoAgenda)
        {
            if (id != eventoAgenda.id_evento)
            {
                return NotFound();
            }

            // Eliminar errores de ModelState para las propiedades de navegación
            ModelState.Remove("Usuario");
            ModelState.Remove("Chofer");

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (eventoAgenda.id_usuario != userId)
            {
                return Unauthorized();
            }

            // Obtener evento original para comparar
            var eventoOriginal = await _context.EventosAgenda.AsNoTracking().FirstOrDefaultAsync(e => e.id_evento == id);
            bool esEventoFuturo = eventoOriginal?.fecha_inicio > DateTime.Now;

            // Validaciones adicionales
            if (esEventoFuturo && eventoAgenda.fecha_inicio < DateTime.Now.Date)
            {
                ModelState.AddModelError("fecha_inicio", "La fecha de inicio no puede ser anterior a la fecha actual.");
            }

            if (eventoAgenda.fecha_fin.HasValue && eventoAgenda.fecha_fin <= eventoAgenda.fecha_inicio)
            {
                ModelState.AddModelError("fecha_fin", "La fecha de fin debe ser posterior a la fecha de inicio.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Configurar valores predeterminados si es necesario
                    if (eventoAgenda.requiere_notificacion && !eventoAgenda.dias_anticipacion_notificacion.HasValue)
                    {
                        eventoAgenda.dias_anticipacion_notificacion = 3; // Valor predeterminado
                    }

                    // Si cambiamos la fecha o tipo y ya se había enviado notificación, podríamos resetearla
                    if (eventoOriginal != null &&
                        (eventoOriginal.fecha_inicio != eventoAgenda.fecha_inicio ||
                         eventoOriginal.tipo_evento != eventoAgenda.tipo_evento))
                    {
                        // Solo reseteamos notificaciones si el evento está pendiente
                        if (eventoAgenda.estado == "Pendiente")
                        {
                            eventoAgenda.notificacion_enviada = false;
                        }
                    }

                    _context.Update(eventoAgenda);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!EventoAgendaExists(eventoAgenda.id_evento))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }


            ViewBag.Choferes = new SelectList(_context.Choferes, "id_chofer", "nombreCompleto", eventoAgenda.id_chofer);
            return View(eventoAgenda);
        }

        private bool EventoAgendaExists(int id)
        {
            return _context.EventosAgenda.Any(e => e.id_evento == id);
        }

        // Implementación de Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var eventoAgenda = await _context.EventosAgenda
                .Include(e => e.Usuario)
                .Include(e => e.Chofer)
                .FirstOrDefaultAsync(e => e.id_evento == id && e.id_usuario == userId);

            if (eventoAgenda == null)
            {
                return NotFound();
            }

            return View(eventoAgenda);
        }

        // Implementación de Delete (GET)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var eventoAgenda = await _context.EventosAgenda
                .Include(e => e.Usuario)
                .Include(e => e.Chofer)
                .FirstOrDefaultAsync(e => e.id_evento == id && e.id_usuario == userId);

            if (eventoAgenda == null)
            {
                return NotFound();
            }

            return View(eventoAgenda);
        }

        // Implementación de Delete (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var eventoAgenda = await _context.EventosAgenda
                .FirstOrDefaultAsync(e => e.id_evento == id && e.id_usuario == userId);

            if (eventoAgenda == null)
            {
                return NotFound();
            }

            _context.EventosAgenda.Remove(eventoAgenda);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private static string GetEventClass(string tipoEvento, string estado)
        {
            if (estado == "Cancelado")
                return "bg-secondary";

            switch (tipoEvento)
            {
                case "Renovación":
                    return "bg-warning";
                case "Mantenimiento":
                    return "bg-info";
                case "Reunión":
                    return "bg-primary";
                case "Vencimiento":
                    return "bg-danger";
                default:
                    return "bg-success";
            }
        }
    }
}
