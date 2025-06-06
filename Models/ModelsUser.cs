﻿using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace COMAVI_SA.Models
{
#nullable disable
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    public class Usuario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_usuario { get; set; }

        [Required]
        [StringLength(50)]
        public string nombre_usuario { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string correo_electronico { get; set; }

        [Required]
        [StringLength(255)]
        public string contrasena { get; set; }

        [Required]
        [StringLength(20)]
        public string rol { get; set; } = "user";

        public DateTime? ultimo_ingreso { get; set; }

        [Required]
        [StringLength(20)]
        public string estado_verificacion { get; set; } = "pendiente";

        public DateTime? fecha_verificacion { get; set; }

        [StringLength(100)]
        public string? token_verificacion { get; set; }

        public DateTime? fecha_expiracion_token { get; set; }

        [Required]
        public DateTime fecha_registro { get; set; } = DateTime.Now;

        public bool mfa_habilitado { get; set; } = false;

        public DateTime? fecha_actualizacion_password { get; set; }

    }

    public class IntentosLogin
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_intento { get; set; }

        public int? id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        public DateTime fecha_hora { get; set; }

        [Required]
        public bool exitoso { get; set; }

        [Required]
        [StringLength(45)]
        public string direccion_ip { get; set; }
    }

    public class SesionesActivas
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_sesion { get; set; }

        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [StringLength(500)]
        public string dispositivo { get; set; }

        [StringLength(500)]
        public string ubicacion { get; set; }

        [Required]
        public DateTime fecha_inicio { get; set; }

        [Required]
        public DateTime fecha_ultima_actividad { get; set; }
    }

    public partial class Notificaciones_Usuario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_notificacion { get; set; }

        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [StringLength(50)]
        public string tipo_notificacion { get; set; }

        [Required]
        public DateTime fecha_hora { get; set; }

        [Required]
        public string mensaje { get; set; }

        public bool? leida { get; set; } = false;


    }

    public class RestablecimientoContrasena
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_reset { get; set; }

        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [StringLength(255)]
        public string token { get; set; }

        [Required]
        public DateTime fecha_solicitud { get; set; }

        [Required]
        public DateTime fecha_expiracion { get; set; }
    }

    public class MFA
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_mfa { get; set; }

        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [StringLength(10)]
        public string codigo { get; set; }

        [Required]
        public DateTime fecha_generacion { get; set; }

        [Required]
        public bool usado { get; set; }

        [Required]
        public bool esta_activo { get; set; } = false;
    }

    public class EventoCalendario
    {
        public string id { get; set; }
        public string title { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string className { get; set; }
        public string description { get; set; }
    }

    public class CodigosRespaldoMFA
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_codigo_respaldo { get; set; }

        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [StringLength(20)]
        public string codigo { get; set; }

        [Required]
        public DateTime fecha_generacion { get; set; }

        [Required]
        public bool usado { get; set; } = false;
    }

    public class PreferenciasNotificacion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_preferencia { get; set; }

        [Required]
        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        [Required]
        [Display(Name = "Notificar por correo electrónico")]
        public bool notificar_por_correo { get; set; } = true;

        [Required]
        [Display(Name = "Días de anticipación para notificar")]
        [Range(1, 60, ErrorMessage = "El valor debe estar entre 1 y 60 días")]
        public int dias_anticipacion { get; set; } = 15;

        [Required]
        [Display(Name = "Notificar vencimiento de licencia")]
        public bool notificar_vencimiento_licencia { get; set; } = true;

        [Required]
        [Display(Name = "Notificar vencimiento de documentos")]
        public bool notificar_vencimiento_documentos { get; set; } = true;
    }

    public class Solicitudes_Mantenimiento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_solicitud { get; set; }
        public int id_camion { get; set; }
        public int id_chofer { get; set; }
        public DateTime fecha_solicitud { get; set; }
        public string observaciones { get; set; }
        public string estado { get; set; } // "pendiente", "aprobado", "rechazado", "completado"
        public string comentario_admin { get; set; }
        public DateTime? fecha_programada { get; set; }
        public DateTime? fecha_completado { get; set; }
        public DateTime? fecha_revision { get; set; }
        public int? id_admin_revisor { get; set; }

        // Propiedades de navegación
        public virtual Camiones Camion { get; set; }
        public virtual Choferes Chofer { get; set; }
        public virtual Usuario AdminRevisor { get; set; }

    }
    public class Choferes
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_chofer { get; set; }

        public int? id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }


        [Required]
        [StringLength(100)]
        public string nombreCompleto { get; set; }

        [Required]
        public int edad { get; set; }

        [Required]
        [StringLength(20)]
        public string numero_cedula { get; set; }

        [Required]
        [StringLength(50)]
        public string licencia { get; set; }

        [Required]
        public DateTime fecha_venc_licencia { get; set; }

        [Required]
        [StringLength(10)]
        public string estado { get; set; }

        [Required]
        [StringLength(10)]
        public string genero { get; set; } // M = masculino, F = femenino asi esta en la base de datos en minuscula


    }

    public class Camiones
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_camion { get; set; }

        [Required]
        [StringLength(50)]
        public string marca { get; set; }

        [Required]
        [StringLength(50)]
        public string modelo { get; set; }

        [Required]
        public int anio { get; set; }

        [Required]
        [StringLength(20)]
        public string numero_placa { get; set; }

        [Required]
        [StringLength(15)]
        public string estado { get; set; } // mantenimiento, activo, inactivo

        public int? chofer_asignado { get; set; }

        [ForeignKey("chofer_asignado")]
        public Choferes? Chofer { get; set; }
    }

    public class Mantenimiento_Camiones
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_mantenimiento { get; set; }

        [Required]
        public int id_camion { get; set; }

        [ForeignKey("id_camion")]
        public Camiones Camion { get; set; }

        [Required]
        public string descripcion { get; set; }

        [Required]
        public DateTime fecha_mantenimiento { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal costo { get; set; }

        public string? moneda { get; set; }
        public string? detalles_costo { get; set; }

    }

    public class Documentos
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_documento { get; set; }

        [Required]
        public int id_chofer { get; set; }

        [ForeignKey("id_chofer")]
        public Choferes Chofer { get; set; }

        [Required]
        [StringLength(50)]
        public string tipo_documento { get; set; }

        [Required]
        public DateTime fecha_emision { get; set; }

        [Required]
        public DateTime fecha_vencimiento { get; set; }

        [StringLength(255)]
        public string? ruta_archivo { get; set; }

        public byte[]? contenido_archivo { get; set; }

        [StringLength(100)]
        public string? tipo_mime { get; set; } = "application/pdf";

        public int? tamano_archivo { get; set; }

        [StringLength(64)]
        public string? hash_documento { get; set; }

        [Required]
        [StringLength(20)]
        public string estado_validacion { get; set; } = "pendiente";
    }

    public class EventoAgenda
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id_evento { get; set; }

        [Required]
        public int id_usuario { get; set; }

        [ForeignKey("id_usuario")]
        public Usuario Usuario { get; set; }

        public int? id_chofer { get; set; }

        [ForeignKey("id_chofer")]
        public Choferes Chofer { get; set; }

        [Required]
        [StringLength(100)]
        public string titulo { get; set; }

        public string descripcion { get; set; }

        [Required]
        public DateTime fecha_inicio { get; set; }

        public DateTime? fecha_fin { get; set; }

        [Required]
        [StringLength(20)]
        public string tipo_evento { get; set; } // "renovacion", "mantenimiento", "reunion", etc.

        [Required]
        [StringLength(20)]
        public string estado { get; set; } // "pendiente", "completado", "cancelado"

        [Required]
        public bool requiere_notificacion { get; set; } = true;

        public int? dias_anticipacion_notificacion { get; set; } = 3;

        public bool notificacion_enviada { get; set; } = false;
    }

    public class CalendarEvent
    {
        public string id { get; set; }
        public string title { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string className { get; set; }
        public string description { get; set; }
        public bool allDay { get; set; } = false;
    }
    
    public class DashboardStats
    {
        public int TotalCamiones { get; set; }
        public int CamionesActivos { get; set; }
        public int TotalChoferes { get; set; }
        public int ChoferesActivos { get; set; }
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
    }


    public class AppSettings
    {
        public string BaseUrl { get; set; }
    }


    //ViewModel

    public class SolicitudMantenimientoViewModel
    {
        // Propiedades existentes
        public int IdSolicitud { get; set; }
        public int IdChofer { get; set; }
        public string NombreChofer { get; set; }
        public int IdCamion { get; set; }
        public string InfoCamion { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string Observaciones { get; set; }
        public string Estado { get; set; }
        public string NombreAdmin { get; set; }
        public DateTime? FechaRevision { get; set; }
        public string DescripcionMantenimiento { get; set; }
        public decimal? Costo { get; set; }
        public string Moneda { get; set; }
        public string DetallesCosto { get; set; }

        // Métodos de ayuda para extraer datos JSON de forma segura
        public decimal GetCostoBase()
        {
            if (string.IsNullOrEmpty(DetallesCosto)) return Costo ?? 0;

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(DetallesCosto);
                return json["costo_base"]?.Value<decimal>() ?? Costo ?? 0;
            }
            catch
            {
                return Costo ?? 0;
            }
        }

        public decimal GetImpuestoIva()
        {
            if (string.IsNullOrEmpty(DetallesCosto)) return 0;

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(DetallesCosto);
                return json["impuesto_iva"]?.Value<decimal>() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class DocumentoVencimientoIndexViewModel
    {
        public int id_documento { get; set; }
        public int id_chofer { get; set; }
        public string nombreCompleto { get; set; }
        public string tipo_documento { get; set; }
        public DateTime fecha_emision { get; set; }
        public DateTime fecha_vencimiento { get; set; }
        public int dias_para_vencimiento { get; set; }
        public string estado_validacion { get; set; }

        // Propiedades calculadas (puedes mantenerlas)
        public string EstadoAlerta => dias_para_vencimiento <= 0 ? "Vencido" :
                                     dias_para_vencimiento <= 7 ? "Crítico" :
                                     dias_para_vencimiento <= 15 ? "Advertencia" : "Normal";
    }

    public class AdminDashboardViewModel
    {
        public int TotalCamiones { get; set; }
        public int CamionesActivos { get; set; }
        public int TotalChoferes { get; set; }
        public int ChoferesActivos { get; set; }
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
        public int DocumentosProximosVencer { get; set; }
        public List<Camiones> Camiones { get; set; } = new List<Camiones>();
        public List<Choferes> Choferes { get; set; } = new List<Choferes>();
        public List<DocumentoVencimientoViewModel> DocumentosProximosVencimiento { get; set; } = new List<DocumentoVencimientoViewModel>();
    }

    public class CamionViewModel
    {
        public int id_camion { get; set; }
        public string marca { get; set; }
        public string modelo { get; set; }
        public int anio { get; set; }
        public string numero_placa { get; set; }
        public string estado { get; set; }
        public int? chofer_asignado { get; set; }
        public string NombreChofer { get; set; }
        public string estado_operativo { get; set; }
        public string? ultima_fecha_mantenimiento { get; set; }
    }

    public class DocumentoVencimientoViewModel
    {
        public int id_documento { get; set; }
        public int id_chofer { get; set; }
        public string nombreCompleto { get; set; } // En el SP se llama "nombreChofer"
        public string tipo_documento { get; set; }
        public DateTime fecha_emision { get; set; }
        public DateTime fecha_vencimiento { get; set; }
        public int dias_para_vencimiento { get; set; }
        public string estado_validacion { get; set; }

        // Propiedad calculada que no está en el SP
        [NotMapped]
        public string estadoDocumento => dias_para_vencimiento <= 0 ? "Vencido" :
                                       dias_para_vencimiento <= 30 ? "Por vencer" : "Vigente";
    }

    public class UsuarioAdminViewModel
    {
        public int id_usuario { get; set; }
        public string nombre_usuario { get; set; }
        public string correo_electronico { get; set; }
        public string rol { get; set; }
        public DateTime? ultimo_ingreso { get; set; }
        public string estado_verificacion { get; set; }
        public DateTime fecha_registro { get; set; }
        public int sesiones_activas { get; set; }
    }

    public class EditarUsuarioViewModel
    {
        public int id_usuario { get; set; }
        public string nombre_usuario { get; set; }
        public string correo_electronico { get; set; }
        public string rol { get; set; }
    }

    public class SesionActivaViewModel
    {
        public int id_sesion { get; set; }
        public int id_usuario { get; set; }
        public string nombre_usuario { get; set; }
        public string dispositivo { get; set; }
        public DateTime fecha_inicio { get; set; }
        public DateTime fecha_ultima_actividad { get; set; }
    }

    public class GraficoDataViewModel
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public string Color { get; set; }
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();

        public string Nombre
        {
            get => Label;
            set => Label = value;
        }

        public int Valor
        {
            get => Value;
            set => Value = value;
        }
    }

    public class ActividadRecienteViewModel
    {
        public string tipo_actividad { get; set; }
        public string descripcion { get; set; }
        public DateTime fecha { get; set; }
        public string detalles { get; set; }

    }

    public class DashboardViewModel
    {
        // Propiedades que coinciden con el SP
        public int TotalCamiones { get; set; }
        public int CamionesActivos { get; set; }
        public int CamionesEnMantenimiento { get; set; }
        public int CamionesInactivos { get; set; } 
        public int TotalChoferes { get; set; }
        public int ChoferesActivos { get; set; }
        public int ChoferesLicenciaPorVencer { get; set; } 
        public int ChoferesLicenciaVencida { get; set; } 
        public int TotalDocumentos { get; set; } 
        public int DocumentosVerificados { get; set; } 
        public int DocumentosPendientes { get; set; } 
        public int DocumentosRechazados { get; set; } 
        public int DocumentosProximosVencer { get; set; }
        public int MantenimientosUltimoMes { get; set; } 
        public decimal CostoMantenimientoUltimoMes { get; set; } 
    }

    public class MantenimientoReporteViewModel
    {
        public int id_mantenimiento { get; set; }
        public int id_camion { get; set; }
        public string marca { get; set; }
        public string modelo { get; set; }
        public string numero_placa { get; set; }
        public string descripcion { get; set; }
        public DateTime fecha_mantenimiento { get; set; }
        public decimal costo { get; set; } // Aseguramos que es decimal
        public string moneda { get; set; } = "CRC";
        public string detalles_costo { get; set; }

        // Propiedades para mostrar detalles de costo - todas como decimal
        public decimal costo_base { get; set; }
        public decimal impuesto_iva { get; set; }
        public decimal otros_costos { get; set; }
        public decimal tipo_cambio { get; set; } = 625m; // Añadimos sufijo 'm' para decimal literal

        // Métodos para obtener símbolos monetarios
        public string SimboloMoneda => moneda == "USD" ? "$" : "₡";

        // Valor total formateado con símbolo
        public string CostoFormateado => $"{SimboloMoneda}{costo:N2}";

        // Método para obtener detalles costo como diccionario
        public Dictionary<string, decimal> ObtenerDetallesCosto()
        {
            if (string.IsNullOrEmpty(detalles_costo))
            {
                return new Dictionary<string, decimal>
            {
                { "costo_base", costo },
                { "impuesto_iva", 0m },
                { "otros_costos", 0m },
                { "tipo_cambio", 625m }
            };
            }

            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, decimal>>(detalles_costo);
            }
            catch
            {
                return new Dictionary<string, decimal>
            {
                { "costo_base", costo },
                { "impuesto_iva", 0m },
                { "otros_costos", 0m },
                { "tipo_cambio", 625m }
            };
            }
        }

        // Método auxiliar para convertir cualquier valor a decimal de manera segura
        public static decimal ToDecimal(object value)
        {
            try
            {
                if (value == null) return 0m;
                return Convert.ToDecimal(value);
            }
            catch
            {
                return 0m;
            }
        }
    }

    public class NotificacionesViewModel
    {
        public List<Notificaciones_Usuario> Notificaciones { get; set; }
        public PreferenciasNotificacion Preferencias { get; set; }
        public int PaginaActual { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;

    }

    public class ResetPasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string Codigo { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

    public class CambiarContrasenaViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña actual es requerida")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña Actual")]
        public string PasswordActual { get; set; }

        [Required(ErrorMessage = "La nueva contraseña es requerida")]
        [StringLength(100, ErrorMessage = "La contraseña debe tener al menos {2} caracteres de longitud.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva Contraseña")]
        public string NuevaPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Nueva Contraseña")]
        [Compare("NuevaPassword", ErrorMessage = "La nueva contraseña y la confirmación no coinciden.")]
        public string ConfirmarPassword { get; set; }
    }

    public class CodigosRespaldoViewModel
    {
        public List<string> Codigos { get; set; }
    }

    public class ConfigurarMFAViewModel
    {
        public string Secret { get; set; }
        public string QrCodeUrl { get; set; }

        [Required(ErrorMessage = "El código OTP es requerido")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "El código OTP debe tener 6 dígitos")]
        public string OtpCode { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public class OtpViewModel
    {
        [Required(ErrorMessage = "El código OTP es requerido")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "El código OTP debe tener 6 dígitos")]
        public string OtpCode { get; set; }

        public string Email { get; set; }
        public int IntentosFallidos { get; set; } = 0;
        public bool UsarCodigoRespaldo { get; set; } = false;
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(50, ErrorMessage = "El nombre de usuario debe tener entre 3 y 50 caracteres", MinimumLength = 3)]
        public string UserName { get; set; }

        [Required(ErrorMessage = "El correo electrónico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        [StringLength(100)]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, ErrorMessage = "La contraseña debe tener al menos 8 caracteres", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "El rol es requerido")]
        public string Role { get; set; }
    }

    public class PerfilViewModel
    {
        [Display(Name = "Nombre de Usuario")]
        public string? NombreUsuario { get; set; }

        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }

        [Display(Name = "Rol")]
        public string? Rol { get; set; }

        [Required(ErrorMessage = "La edad es obligatoria")]
        [Range(18, 99, ErrorMessage = "La edad debe estar entre 18 y 99 años")]
        public int? Edad { get; set; }

        [Required(ErrorMessage = "El género es obligatorio")]
        public string Genero { get; set; }

        [Required(ErrorMessage = "El número de cédula es obligatorio")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "El número de cédula debe tener entre 6 y 20 caracteres")]
        [Display(Name = "Número de Cédula")]
        public string Numero_Cedula { get; set; }

        [Required(ErrorMessage = "El número de licencia es obligatorio")]
        [StringLength(50, ErrorMessage = "El número de licencia no puede exceder los 50 caracteres")]
        public string Licencia { get; set; }

        [Required(ErrorMessage = "La fecha de vencimiento es obligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Vencimiento")]
        public DateTime? Fecha_Venc_Licencia { get; set; }

        [Required]
        [StringLength(10)]
        public string? Estado { get; set; }

        [Display(Name = "Último Ingreso")]
        public DateTime? UltimoIngreso { get; set; }

        [Display(Name = "MFA Habilitado")]
        public bool MfaHabilitado { get; set; }

        [Display(Name = "Última Actualización de Contraseña")]
        public DateTime? FechaActualizacionPassword { get; set; }
    }

    public class ChoferViewModel
    {
        public int id_chofer { get; set; }
        public string nombreCompleto { get; set; }
        public int edad { get; set; }
        public string numero_cedula { get; set; }
        public string licencia { get; set; }
        public DateTime fecha_venc_licencia { get; set; }
        public string estado { get; set; }
        public string genero { get; set; }
        public int dias_para_vencimiento { get; set; }
        public string estado_licencia { get; set; }
        public int? id_camion { get; set; }
        public string camion_asignado { get; set; }
        public int total_documentos { get; set; }
        public int? numero_registro { get; set; } // Puede ser nulo en algunos casos
    }

    public class VerificacionViewModel
    {
        [Required(ErrorMessage = "El token es requerido")]
        public string Token { get; set; }

        [Required(ErrorMessage = "El correo electrónico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        public string Email { get; set; }
    }

    public class CargaDocumentoViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un tipo de documento")]
        public string TipoDocumento { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un archivo PDF")]
        public IFormFile ArchivoPdf { get; set; }

        [Required(ErrorMessage = "La fecha de emisión es requerida")]
        [DataType(DataType.Date)]
        public DateTime FechaEmision { get; set; }

        [Required(ErrorMessage = "La fecha de vencimiento es requerida")]
        [DataType(DataType.Date)]
        public DateTime FechaVencimiento { get; set; }

        public int? IdChofer { get; set; }

        public List<Documentos> DocumentosExistentes { get; set; } = new List<Documentos>();

        public List<string> DocumentosFaltantes { get; set; } = new List<string>();


    }

    public class InstruccionesVerificacionViewModel
    {
        public string Email { get; set; }
        public string NombreUsuario { get; set; }
        public string PasosVerificacion { get; set; }
        public string TokenVerificacion { get; set; }
        public DateTime FechaExpiracion { get; set; }
    }

    public class MantenimientoNotificacionViewModel
    {
        // ID del mantenimiento
        public int id_mantenimiento { get; set; }

        // ID del camión
        public int id_camion { get; set; }

        // Datos del camión
        public string numero_placa { get; set; }
        public string marca { get; set; }
        public string modelo { get; set; }

        // Datos del mantenimiento
        public string descripcion { get; set; }
        public string? fecha_mantenimiento { get; set; }

        // Campos calculados desde la BD
        public int dias_restantes { get; set; }
        public string estado_camion { get; set; }
        public bool es_hoy { get; set; }

        // Propiedades calculadas adicionales para la UI
        [NotMapped]
        public string EstadoAlerta =>
            es_hoy ? "danger" :
            dias_restantes <= 7 ? "danger" :
            dias_restantes <= 15 ? "warning" : "info";


        [NotMapped]
        public string EstadoTexto =>
            es_hoy ? "HOY" :
            $"{dias_restantes} días";
    }
}