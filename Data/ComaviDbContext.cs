using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;

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
        }
    }
}