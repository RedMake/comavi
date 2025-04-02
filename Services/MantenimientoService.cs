using COMAVI_SA.Models;
using COMAVI_SA.Repository;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COMAVI_SA.Services
{
#pragma warning disable CS0168

    public interface IMantenimientoService
    {
        Task<bool> ProgramarMantenimientoAsync(Mantenimiento_Camiones mantenimiento);
        Task NotificarMantenimientosAsync();
        Task ActualizarEstadosCamionesAsync();
        Task<List<MantenimientoNotificacionViewModel>> GetMantenimientosProgramadosAsync(int diasAntelacion = 30);
    }

    public class MantenimientoService : IMantenimientoService
    {
        private readonly IDatabaseRepository _databaseRepository;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public MantenimientoService(
            IDatabaseRepository databaseRepository,
            IEmailService emailService,
            INotificationService notificationService,
            IUserService userService)
        {
            _databaseRepository = databaseRepository;
            _emailService = emailService;
            _notificationService = notificationService;
            _userService = userService;
        }

        public async Task<bool> ProgramarMantenimientoAsync(Mantenimiento_Camiones mantenimiento)
        {
            try
            {
                // Registrar el mantenimiento en la base de datos
                var resultado = await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarMantenimiento",
                    new
                    {
                        id_camion = mantenimiento.id_camion,
                        descripcion = mantenimiento.descripcion,
                        fecha_mantenimiento = mantenimiento.fecha_mantenimiento,
                        costo = mantenimiento.costo,
                        moneda = mantenimiento.moneda,
                        detalles_costo = mantenimiento.detalles_costo
                    }
                );

                if (resultado <= 0)
                {
                    return false;
                }

                // Obtener información del camión
                var camion = await _databaseRepository.ExecuteScalarProcedureAsync<Camiones>(
                    "sp_ObtenerCamionPorId",
                    new { id_camion = mantenimiento.id_camion }
                );

                if (camion == null)
                {
                    return false;
                }

                // Notificar a todos los usuarios sobre el nuevo mantenimiento programado
                await NotificarNuevoMantenimientoAsync(camion, mantenimiento);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task NotificarMantenimientosAsync()
        {
            try
            {
                // Obtener todos los mantenimientos programados para hoy
                var mantenimientosHoy = await _databaseRepository.ExecuteQueryProcedureAsync<dynamic>(
                    "sp_ObtenerMantenimientosParaHoy"
                );

                if (mantenimientosHoy == null || !mantenimientosHoy.Any())
                {
                    return;
                }

                // Obtener todos los usuarios para notificarlos
                var usuarios = await _userService.GetAllUsersAsync();

                foreach (var mantenimiento in mantenimientosHoy)
                {
                    try
                    {
                        int idMantenimiento = mantenimiento.id_mantenimiento;
                        int idCamion = mantenimiento.id_camion;
                        string numeroPlaca = mantenimiento.numero_placa;
                        string marcaModelo = $"{mantenimiento.marca} {mantenimiento.modelo}";
                        string descripcion = mantenimiento.descripcion;

                        string mensaje = $"RECORDATORIO: Hoy se realizará mantenimiento al camión {marcaModelo} (Placa: {numeroPlaca}). Descripción: {descripcion}";
                        string asunto = $"Mantenimiento Programado para Hoy - Camión {numeroPlaca}";

                        // Notificar a cada usuario en la aplicación
                        foreach (var usuario in usuarios)
                        {
                            await _notificationService.CreateNotificationAsync(
                                usuario.id_usuario,
                                "Mantenimiento Programado",
                                mensaje
                            );
                        }

                        // Enviar correo electrónico a todos los usuarios
                        foreach (var usuario in usuarios)
                        {
                            await _emailService.EnviarCorreoAsync(
                                usuario.correo_electronico,
                                asunto,
                                mensaje
                            );
                        }

                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task ActualizarEstadosCamionesAsync()
        {
            try
            {
                // Ejecutar el procedimiento que actualiza los estados
                var camionesActualizados = await _databaseRepository.ExecuteQueryProcedureAsync<dynamic>(
                    "sp_ActualizarEstadoMantenimiento"
                );

                if (camionesActualizados == null || !camionesActualizados.Any())
                {
                    return;
                }

               

                // Notificar a los usuarios sobre los cambios de estado
                var usuarios = await _userService.GetAllUsersAsync();
                foreach (var camion in camionesActualizados)
                {
                    string mensaje = camion.estado == "mantenimiento"
                        ? $"El camión {camion.numero_placa} ha entrado en mantenimiento hoy."
                        : $"El camión {camion.numero_placa} ha finalizado su mantenimiento y está activo nuevamente.";

                    foreach (var usuario in usuarios)
                    {
                        await _notificationService.CreateNotificationAsync(
                            usuario.id_usuario,
                            "Estado de Camión Actualizado",
                            mensaje
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<MantenimientoNotificacionViewModel>> GetMantenimientosProgramadosAsync(int diasAntelacion = 30)
        {
            try
            {
                var mantenimientos = await _databaseRepository.ExecuteQueryProcedureAsync<MantenimientoNotificacionViewModel>(
                    "sp_ObtenerNotificacionesMantenimiento",
                    new { dias_antelacion = diasAntelacion }
                );

                return mantenimientos.ToList();
            }
            catch (Exception ex)
            {
                return new List<MantenimientoNotificacionViewModel>();
            }
        }

        private async Task NotificarNuevoMantenimientoAsync(Camiones camion, Mantenimiento_Camiones mantenimiento)
        {
            try
            {
                // Obtener todos los usuarios para notificarlos
                var usuarios = await _userService.GetAllUsersAsync();

                string fechaMantenimiento = mantenimiento.fecha_mantenimiento.ToString("dd/MM/yyyy");
                string mensaje = $"Se ha programado mantenimiento para el camión {camion.marca} {camion.modelo} (Placa: {camion.numero_placa}) " +
                                $"para el día {fechaMantenimiento}.";

                string asunto = $"Nuevo Mantenimiento Programado - Camión {camion.numero_placa}";

                // Notificar a cada usuario en la aplicación
                foreach (var usuario in usuarios)
                {
                    await _notificationService.CreateNotificationAsync(
                        usuario.id_usuario,
                        "Mantenimiento Programado",
                        mensaje
                    );
                }

                // Enviar correo electrónico a todos los usuarios
                foreach (var usuario in usuarios)
                {
                    await _emailService.EnviarCorreoAsync(
                        usuario.correo_electronico,
                        asunto,
                        mensaje
                    );
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}