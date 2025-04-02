using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;

namespace COMAVI_SA.Data
{
    public class ComaviDbContext : DbContext
    {
        public ComaviDbContext(DbContextOptions<ComaviDbContext> options) : base(options)
        {
        }

        public virtual DbSet<Usuario> Usuarios { get; set; }
        public virtual DbSet<IntentosLogin> IntentosLogin { get; set; }
        public virtual DbSet<SesionesActivas> SesionesActivas { get; set; }
        public virtual DbSet<Notificaciones_Usuario> NotificacionesUsuario { get; set; }
        public virtual DbSet<RestablecimientoContrasena> RestablecimientoContrasena { get; set; }
        public virtual DbSet<MFA> MFA { get; set; }
        public virtual DbSet<CodigosRespaldoMFA> CodigosRespaldoMFA { get; set; } 
        public virtual DbSet<Choferes> Choferes { get; set; }
        public virtual DbSet<Camiones> Camiones { get; set; }
        public virtual DbSet<Documentos> Documentos { get; set; }
        public virtual DbSet<Mantenimiento_Camiones> Mantenimiento_Camiones { get; set; }
        public virtual DbSet<PreferenciasNotificacion> PreferenciasNotificacion { get; set; }
        public virtual DbSet<EventoAgenda> EventosAgenda { get; set; }
        public virtual DbSet<Solicitudes_Mantenimiento> Solicitudes_Mantenimiento { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración explícita de tablas
            modelBuilder.Entity<Usuario>().ToTable("Usuario");
            modelBuilder.Entity<IntentosLogin>().ToTable("IntentosLogin");
            modelBuilder.Entity<SesionesActivas>().ToTable("SesionesActivas");
            modelBuilder.Entity<Notificaciones_Usuario>().ToTable("Notificaciones_Usuario");
            modelBuilder.Entity<RestablecimientoContrasena>().ToTable("RestablecimientoContrasena");
            modelBuilder.Entity<MFA>().ToTable("MFA");
            modelBuilder.Entity<Choferes>().ToTable("Choferes");
            modelBuilder.Entity<Camiones>().ToTable("Camiones");
            modelBuilder.Entity<Documentos>().ToTable("Documentos");
            modelBuilder.Entity<Mantenimiento_Camiones>().ToTable("Mantenimiento_Camiones");
            modelBuilder.Entity<CodigosRespaldoMFA>().ToTable("CodigosRespaldoMFA");
            modelBuilder.Entity<PreferenciasNotificacion>().ToTable("PreferenciasNotificacion");
            modelBuilder.Entity<EventoAgenda>().ToTable("EventosAgenda");
            modelBuilder.Entity<Solicitudes_Mantenimiento>().ToTable("Solicitudes_Mantenimiento");

            // Índices para mejorar el rendimiento
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.correo_electronico)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .Property(u => u.mfa_habilitado)
                .HasDefaultValue(false);

            modelBuilder.Entity<CodigosRespaldoMFA>()
                .HasIndex(c => new { c.id_usuario, c.codigo });

            modelBuilder.Entity<MFA>()
                .HasIndex(m => new { m.id_usuario, m.esta_activo });

            modelBuilder.Entity<IntentosLogin>()
                .HasIndex(l => new { l.id_usuario, l.fecha_hora });

            modelBuilder.Entity<Camiones>()
                .HasIndex(c => c.numero_placa)
                .IsUnique();

            modelBuilder.Entity<Choferes>()
                .HasIndex(c => c.numero_cedula)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .Property(u => u.estado_verificacion)
                .HasDefaultValue("pendiente");

            modelBuilder.Entity<Documentos>()
                .Property(d => d.estado_validacion)
                .HasDefaultValue("pendiente");

            modelBuilder.Entity<Documentos>()
                .Property(d => d.tipo_mime)
                .HasDefaultValue("application/pdf");

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.token_verificacion);

            modelBuilder.Entity<Documentos>()
                .HasIndex(d => d.estado_validacion);

            modelBuilder.Entity<PreferenciasNotificacion>()
                .HasIndex(p => p.id_usuario)
                .IsUnique();

            modelBuilder.Entity<EventoAgenda>()
                .HasIndex(e => e.fecha_inicio);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasIndex(s => s.id_chofer);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasIndex(s => s.id_camion);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasIndex(s => s.estado);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasIndex(s => s.fecha_solicitud);


            // Relaciones que no están explícitamente definidas en las propiedades y demas configuraciones de las entidades
            modelBuilder.Entity<Camiones>()
                .HasOne(c => c.Chofer)
                .WithMany()
                .HasForeignKey(c => c.chofer_asignado)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Documentos>()
                .HasOne(d => d.Chofer)
                .WithMany()
                .HasForeignKey(d => d.id_chofer)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Mantenimiento_Camiones>()
                .HasOne(m => m.Camion)
                .WithMany()
                .HasForeignKey(m => m.id_camion)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Mantenimiento_Camiones>()
                .Property(m => m.costo)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Choferes>()
                .HasOne(c => c.Usuario)
                .WithOne()
                .HasForeignKey<Choferes>(c => c.id_usuario)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PreferenciasNotificacion>()
                .HasOne(p => p.Usuario)
                .WithOne()
                .HasForeignKey<PreferenciasNotificacion>(p => p.id_usuario)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasOne(s => s.Camion)
                .WithMany()
                .HasForeignKey(s => s.id_camion)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .HasOne(s => s.Chofer)
                .WithMany()
                .HasForeignKey(s => s.id_chofer)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .Property(s => s.estado)
                .HasDefaultValue("pendiente");

            modelBuilder.Entity<Solicitudes_Mantenimiento>()
                .Property(s => s.fecha_solicitud)
                .HasDefaultValueSql("GETDATE()");

            //modelBuilder.Entity<Usuario>()
            //    .Property(u => u.mfa_habilitado)
            //    .HasDefaultValue(false);
        }
    }
}