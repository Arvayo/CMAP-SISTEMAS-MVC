using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Data
{
    public class Cmap54SistemasContext : DbContext
    {
        public Cmap54SistemasContext(DbContextOptions<Cmap54SistemasContext> options)
            : base(options)
        {
        }

        /* =========================================================
         * DbSets
         * ========================================================= */
        public DbSet<TablaDeSocio> TABLA_DE_SOCIOS { get; set; } = null!;
        public DbSet<RptEdoCuenta> RptEdoCuenta { get; set; } = null!;

        public DbSet<TablaDePrestamo> TABLA_DE_PRESTAMOS { get; set; } = null!;
        public DbSet<TablaDeTiposDePrestamo> TABLA_DE_TIPOS_DE_PRESTAMOS { get; set; } = null!;
        public DbSet<DetalleDeTiposDePrestamo> DETALLE_DE_TIPOS_DE_PRESTAMOS { get; set; } = null!;

        /* =========================================================
         * Model Creating
         * ========================================================= */
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            /* ============================
             * SOCIOS
             * ============================ */
            modelBuilder.Entity<TablaDeSocio>(entity =>
            {
                entity.ToTable("TABLA_DE_SOCIOS");
                entity.HasNoKey();
            });

            /* ============================
             * REPORTE: rptedocuenta
             * ============================ */
            modelBuilder.Entity<RptEdoCuenta>(entity =>
            {
                entity.ToTable("rptedocuenta");
                entity.HasNoKey();

                entity.Property(e => e.IdReporte).HasColumnName("IdReporte");
                entity.Property(e => e.TipoPrestamo).HasColumnName("TipoPrestamo");
                entity.Property(e => e.ClavePension).HasColumnName("CLAVEPENSION");
                entity.Property(e => e.NombrePrestamo).HasColumnName("NombrePrestamo");
                entity.Property(e => e.CLAVERENOVACION).HasColumnName("CLAVERENOVACION");
                entity.Property(e => e.PORCENRENOVA).HasColumnName("PORCENRENOVA");
                entity.Property(e => e.FechaPrestamo).HasColumnName("FechaPrestamo");
                entity.Property(e => e.ImportePrestamo).HasColumnName("ImportePrestamo");
                entity.Property(e => e.PlazoMeses).HasColumnName("PlazoMeses");
                entity.Property(e => e.FechaVencimiento).HasColumnName("FechaVencimiento");

                entity.Property(e => e.Saldo).HasColumnName("Saldo");
                entity.Property(e => e.CantidadPuedeSolicitar).HasColumnName("CantidadPuedeSolicitar");
                entity.Property(e => e.ImporteLiquido).HasColumnName("ImporteLiquido");
                entity.Property(e => e.Descuentos).HasColumnName("Descuentos");

                entity.Property(e => e.Subcve).HasColumnName("Subcve");
                entity.Property(e => e.Nombresocio).HasColumnName("NOMBRESOCIO");
                entity.Property(e => e.Apellidossocio).HasColumnName("APELLIDOSSOCIO");

                entity.Property(e => e.Saldoahorros).HasColumnName("SALDOAHORROS");
                entity.Property(e => e.Sueldonetototal).HasColumnName("SUELDONETOTOTAL");

                entity.Property(e => e.Id).HasColumnName("Id");

                entity.Property(e => e.Ahorroper).HasColumnName("Ahorroper");
                entity.Property(e => e.Tercer_linea_1).HasColumnName("Tercer_linea_1");
                entity.Property(e => e.Tercer_linea_2).HasColumnName("Tercer_linea_2");
                entity.Property(e => e.Cuarta_linea_1).HasColumnName("Cuarta_linea_1");
                entity.Property(e => e.Cuarta_linea_2).HasColumnName("Cuarta_linea_2");
                entity.Property(e => e.Quinta_linea_1).HasColumnName("Quinta_linea_1");

                entity.Property(e => e.Catsocio).HasColumnName("Catsocio");
                entity.Property(e => e.Folio).HasColumnName("Folio");

                entity.Property(e => e.LiquidaCon).HasColumnName("LiquidaCon");
            });

            modelBuilder.Entity<TablaDePrestamo>()
                .ToTable("TABLA_DE_PRESTAMOS")
                .HasNoKey();

            modelBuilder.Entity<TablaDeTiposDePrestamo>()
                .ToTable("TABLA_DE_TIPOS_DE_PRESTAMOS")
                .HasNoKey();

            modelBuilder.Entity<DetalleDeTiposDePrestamo>()
                .ToTable("DETALLE_DE_TIPOS_DE_PRESTAMOS")
                .HasNoKey();
        }
    }
}


