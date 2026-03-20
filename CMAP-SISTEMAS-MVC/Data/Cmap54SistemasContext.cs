using CMAP_SISTEMAS_MVC.Models;
using CMAP_SISTEMAS_MVC.Models.DTOs;
using CMAP_SISTEMAS_MVC.Models.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CMAP_SISTEMAS_MVC.Data
{
    /// <summary>
    /// ============================================================
    /// DbContext principal del sistema CMAP54SISTEMAS.
    /// ------------------------------------------------------------
    /// Responsabilidades:
    /// - Exponer las entidades de lectura usadas por el sistema.
    /// - Configurar tablas, vistas y proyecciones SQL (Keyless).
    /// - Centralizar el mapeo entre clases C# y objetos SQL Server.
    ///
    /// Nota importante:
    /// - En este proyecto varias entidades están configuradas como
    ///   HasNoKey() porque se usan principalmente para consultas,
    ///   reportes o lecturas heredadas del sistema VB.
    /// ============================================================
    /// </summary>
    public class Cmap54SistemasContext : DbContext
    {
        /// <summary>
        /// Constructor que recibe las opciones del DbContext
        /// configuradas en Program.cs / Dependency Injection.
        /// </summary>
        public Cmap54SistemasContext(DbContextOptions<Cmap54SistemasContext> options)
            : base(options)
        {
        }

        /* =========================================================
         * DBSETS
         * ---------------------------------------------------------
         * Cada DbSet representa una fuente de datos consultable
         * desde Entity Framework Core.
         * ========================================================= */

        /// <summary>
        /// Tabla de socios.
        /// </summary>
        public DbSet<TablaDeSocio> TABLA_DE_SOCIOS { get; set; } = null!;

        /// <summary>
        /// Tabla o vista de reporte de estado de cuenta.
        /// </summary>
        public DbSet<RptEdoCuenta> RptEdoCuenta { get; set; } = null!;

        /// <summary>
        /// Tabla de préstamos.
        /// </summary>
        public DbSet<TablaDePrestamo> TABLA_DE_PRESTAMOS { get; set; } = null!;

        /// <summary>
        /// Catálogo general de tipos de préstamo.
        /// </summary>
        public DbSet<TablaDeTiposDePrestamo> TABLA_DE_TIPOS_DE_PRESTAMOS { get; set; } = null!;

        /// <summary>
        /// Detalle de tipos de préstamo por tipo de socio / vigencia.
        /// </summary>
        public DbSet<DetalleDeTiposDePrestamo> DETALLE_DE_TIPOS_DE_PRESTAMOS { get; set; } = null!;

        /// <summary>
        /// Proyección SQL keyless para préstamos vigentes con campos
        /// calculados como Liquidacon.
        /// No representa una tabla física.
        /// </summary>
        public DbSet<PrestamoVigenteSqlRow> PrestamosVigentesSql { get; set; } = null!;

        /// <summary>
        /// ============================================================
        /// Proyección SQL Keyless para resultados de funciones escalares.
        /// ------------------------------------------------------------
        /// Este DbSet se utiliza para ejecutar funciones SQL del sistema
        /// que devuelven un valor numérico (decimal).
        ///
        /// Ejemplos de funciones utilizadas en el Estado de Cuenta:
        ///     - dbo.fu_liquidacon(...)
        ///     - dbo.fu_calcular_moratorios(...)
        ///
        /// Entity Framework Core requiere una clase de proyección para
        /// mapear el resultado de funciones SQL cuando se ejecutan mediante:
        ///
        ///     FromSqlRaw()
        ///     FromSqlInterpolated()
        ///
        /// Nota:
        /// - No corresponde a una tabla física.
        /// - Solo se usa como contenedor para recibir el resultado
        ///   de la función SQL.
        /// ============================================================
        /// </summary>
        public DbSet<SqlFuncionResult> SqlFuncionResults { get; set; } = null!;

        /* =========================================================
         * MODEL CREATING
         * ---------------------------------------------------------
         * Aquí se define cómo se mapean las clases a tablas, vistas
         * o proyecciones SQL.
         * ========================================================= */
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigurarTablaDeSocios(modelBuilder);
            ConfigurarReporteEstadoCuenta(modelBuilder);
            ConfigurarPrestamoVigenteSqlRow(modelBuilder);
            ConfigurarTablaDePrestamos(modelBuilder);
            ConfigurarTablaDeTiposDePrestamo(modelBuilder);
            ConfigurarDetalleDeTiposDePrestamo(modelBuilder);
            ConfigurarSqlFuncionResults(modelBuilder);
        }

        /* =========================================================
         * CONFIGURACIONES PRIVADAS
         * ========================================================= */

        /// <summary>
        /// Configuración de TABLA_DE_SOCIOS.
        /// </summary>
        private static void ConfigurarTablaDeSocios(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TablaDeSocio>(entity =>
            {
                entity.ToTable("TABLA_DE_SOCIOS");
                entity.HasNoKey();
            });
        }

        /// <summary>
        /// Configuración del reporte rptedocuenta.
        /// </summary>
        private static void ConfigurarReporteEstadoCuenta(ModelBuilder modelBuilder)
        {
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
        }

        /// <summary>
        /// Configuración de la proyección SQL keyless usada para
        /// consultar préstamos vigentes con campos calculados.
        /// </summary>
        private static void ConfigurarPrestamoVigenteSqlRow(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PrestamoVigenteSqlRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null); // No está ligado a tabla ni vista física
            });
        }

        /// <summary>
        /// Configuración de TABLA_DE_PRESTAMOS.
        /// </summary>
        private static void ConfigurarTablaDePrestamos(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TablaDePrestamo>(entity =>
            {
                entity.ToTable("TABLA_DE_PRESTAMOS");
                entity.HasNoKey();
            });
        }

        /// <summary>
        /// Configuración de TABLA_DE_TIPOS_DE_PRESTAMOS.
        /// </summary>
        private static void ConfigurarTablaDeTiposDePrestamo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TablaDeTiposDePrestamo>(entity =>
            {
                entity.ToTable("TABLA_DE_TIPOS_DE_PRESTAMOS");
                entity.HasNoKey();
            });
        }

        /// <summary>
        /// Configuración de DETALLE_DE_TIPOS_DE_PRESTAMOS.
        /// </summary>
        private static void ConfigurarDetalleDeTiposDePrestamo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DetalleDeTiposDePrestamo>(entity =>
            {
                entity.ToTable("DETALLE_DE_TIPOS_DE_PRESTAMOS");
                entity.HasNoKey();
            });
        }


        /// <summary>
        /// ============================================================
        /// Configuración de la proyección SQL utilizada para recibir
        /// resultados de funciones escalares del sistema.
        /// 
        /// Este modelo es Keyless porque:
        /// - No corresponde a una tabla física.
        /// - Solo se utiliza como contenedor para recibir resultados
        ///   de funciones SQL como:
        ///       dbo.fu_liquidacon(...)
        ///       dbo.fu_calcular_moratorios(...)
        /// ============================================================
        /// </summary>
        private static void ConfigurarSqlFuncionResults(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SqlFuncionResult>(entity =>
            {
                entity.HasNoKey();

                // Indica a EF Core que no está asociado a tabla ni vista
                entity.ToView(null);
            });
        }
    }
}


