using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.NombreUsuario)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
