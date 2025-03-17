using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;

namespace COMAVI_SA.Data
{
    public class ComaviDbContext : DbContext
    {
        public ComaviDbContext(DbContextOptions<ComaviDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<IntentosLogin> IntentosLogin { get; set; }
        public DbSet<SesionesActivas> SesionesActivas { get; set; }
        public DbSet<Notificaciones_Usuario> NotificacionesUsuario { get; set; }
        public DbSet<RestablecimientoContrasena> RestablecimientoContrasena { get; set; }
        public DbSet<MFA> MFA { get; set; }
        public DbSet<Choferes> Choferes { get; set; }
        public DbSet<Camiones> Camiones { get; set; }
        public DbSet<Documentos> Documentos { get; set; }
        public DbSet<Mantenimiento_Camiones> Mantenimiento_Camiones { get; set; }

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

            // Índices para mejorar el rendimiento
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.correo_electronico)
                .IsUnique();

            modelBuilder.Entity<IntentosLogin>()
                .HasIndex(l => new { l.id_usuario, l.fecha_hora });

            modelBuilder.Entity<Camiones>()
                .HasIndex(c => c.numero_placa)
                .IsUnique();

            modelBuilder.Entity<Choferes>()
                .HasIndex(c => c.numero_cedula)
                .IsUnique();

            // Relaciones que no están explícitamente definidas en las propiedades
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

            // Datos semilla para usuario administrador
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin123!"); // Contraseña inicial para el administrador

            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    id_usuario = 1,
                    nombre_usuario = "Administrador",
                    correo_electronico = "admin@docktrack.lat",
                    contrasena = hashedPassword,
                    rol = "admin",
                    ultimo_ingreso = null
                }
            );
        }
    }
}