using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;

namespace COMAVI_SA.Controllers
{
    [AllowAnonymous]

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ComaviDbContext _context;
        private readonly string _connectionString;

        public HomeController(ILogger<HomeController> logger, ComaviDbContext context, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new DashboardViewModel();

                // Estadísticas básicas
                viewModel.TotalCamiones = await _context.Camiones.CountAsync();
                viewModel.CamionesActivos = await _context.Camiones.CountAsync(c => c.estado == "activo");
                viewModel.TotalChoferes = await _context.Choferes.CountAsync();
                viewModel.ChoferesActivos = await _context.Choferes.CountAsync(c => c.estado == "activo");

                // Verificar si el usuario actual es administrador
                var esAdmin = User.IsInRole("admin");
                viewModel.EsUsuarioAdmin = esAdmin;

                if (esAdmin)
                {
                    // Cargar datos para administradores

                    // Próximos vencimientos de documentos
                    viewModel.ProximosVencimientos = await _context.Documentos
                        .Include(d => d.Chofer)
                        .Where(d => d.fecha_vencimiento.Date >= DateTime.Today.Date &&
                                   d.fecha_vencimiento.Date <= DateTime.Today.AddDays(30).Date)
                        .OrderBy(d => d.fecha_vencimiento)
                        .Take(5)
                        .ToListAsync();

                    // Próximos mantenimientos
                    viewModel.ProximosMantenimientos = await _context.Mantenimiento_Camiones
                        .Include(m => m.Camion)
                        .Where(m => m.fecha_mantenimiento.Date >= DateTime.Today.Date)
                        .OrderBy(m => m.fecha_mantenimiento)
                        .Take(5)
                        .ToListAsync();

                    // Datos para gráficos
                    using (var connection = CreateConnection())
                    {
                        // Estado de camiones (activos vs inactivos)
                        viewModel.EstadoCamiones = await connection.QueryAsync<EstadisticaDto>(
                            "SELECT estado as Categoria, COUNT(*) as Cantidad FROM Camiones GROUP BY estado");

                        // Camiones por marca
                        viewModel.CamionesPorMarca = await connection.QueryAsync<EstadisticaDto>(
                            "SELECT marca as Categoria, COUNT(*) as Cantidad FROM Camiones GROUP BY marca");

                        // Choferes por género
                        viewModel.ChoferesPorGenero = await connection.QueryAsync<EstadisticaDto>(
                            "SELECT genero as Categoria, COUNT(*) as Cantidad FROM Choferes GROUP BY genero");

                        // Documentos por tipo
                        viewModel.DocumentosPorTipo = await connection.QueryAsync<EstadisticaDto>(
                            "SELECT tipo_documento as Categoria, COUNT(*) as Cantidad FROM Documentos GROUP BY tipo_documento");
                    }

                    // Camiones sin chofer asignado
                    viewModel.CamionesSinChofer = await _context.Camiones
                        .CountAsync(c => c.chofer_asignado == null && c.estado == "activo");

                    // Usuarios activos
                    viewModel.UsuariosActivos = await _context.SesionesActivas.CountAsync();
                }
                else
                {
                    // Cargar datos específicos para el rol del usuario
                    var esChofer = User.IsInRole("user");
                    if (esChofer)
                    {
                        // Si es chofer, cargar información sobre su camión asignado y documentos
                        var userEmail = User.Identity.Name;
                        var chofer = await _context.Choferes
                            .FirstOrDefaultAsync(c => c.numero_cedula == userEmail);

                        if (chofer != null)
                        {
                            viewModel.InformacionChofer = chofer;

                            // Camión asignado
                            viewModel.CamionAsignado = await _context.Camiones
                                .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                            // Documentos del chofer
                            viewModel.DocumentosChofer = await _context.Documentos
                                .Where(d => d.id_chofer == chofer.id_chofer)
                                .OrderBy(d => d.fecha_vencimiento)
                                .ToListAsync();
                        }
                    }
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard");
                TempData["Error"] = "Error al cargar los datos del dashboard";
                return View(new DashboardViewModel());
            }
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class DashboardViewModel
    {
        // Estadísticas básicas
        public int TotalCamiones { get; set; }
        public int CamionesActivos { get; set; }
        public int TotalChoferes { get; set; }
        public int ChoferesActivos { get; set; }

        // Indicador de rol
        public bool EsUsuarioAdmin { get; set; }

        // Datos para administradores
        public IEnumerable<Documentos> ProximosVencimientos { get; set; } = Enumerable.Empty<Documentos>();
        public IEnumerable<Mantenimiento_Camiones> ProximosMantenimientos { get; set; } = Enumerable.Empty<Mantenimiento_Camiones>();

        // Datos para gráficos
        public IEnumerable<EstadisticaDto> EstadoCamiones { get; set; } = Enumerable.Empty<EstadisticaDto>();
        public IEnumerable<EstadisticaDto> CamionesPorMarca { get; set; } = Enumerable.Empty<EstadisticaDto>();
        public IEnumerable<EstadisticaDto> ChoferesPorGenero { get; set; } = Enumerable.Empty<EstadisticaDto>();
        public IEnumerable<EstadisticaDto> DocumentosPorTipo { get; set; } = Enumerable.Empty<EstadisticaDto>();

        // Más datos para administradores
        public int CamionesSinChofer { get; set; }
        public int UsuariosActivos { get; set; }

        // Datos para choferes
        public Choferes InformacionChofer { get; set; }
        public Camiones CamionAsignado { get; set; }
        public IEnumerable<Documentos> DocumentosChofer { get; set; } = Enumerable.Empty<Documentos>();
    }

    public class EstadisticaDto
    {
        public string Categoria { get; set; }
        public int Cantidad { get; set; }
    }
}