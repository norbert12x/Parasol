using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Models.MapOrganizations;

namespace ParasolBackEnd.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Organizacja> Organizacje { get; set; }
        public DbSet<Adres> Adresy { get; set; }
        public DbSet<Koordynaty> Koordynaty { get; set; }
        public DbSet<Kategoria> Kategorie { get; set; }
        public DbSet<OrganizacjaKategoria> OrganizacjaKategorie { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguracja tabeli Organizacja
            modelBuilder.Entity<Organizacja>(entity =>
            {
                entity.ToTable("organizacja");
                entity.HasKey(e => e.NumerKrs);
                entity.Property(e => e.NumerKrs).HasColumnName("numerkrs");
                entity.Property(e => e.Nazwa).HasColumnName("nazwa");
            });

            // Konfiguracja tabeli Adres
            modelBuilder.Entity<Adres>(entity =>
            {
                entity.ToTable("adres");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.NumerKrs).HasColumnName("numerkrs");
                entity.Property(e => e.Ulica).HasColumnName("ulica");
                entity.Property(e => e.NrDomu).HasColumnName("nrdomu");
                entity.Property(e => e.NrLokalu).HasColumnName("nrlokalu");
                entity.Property(e => e.Miejscowosc).HasColumnName("miejscowosc");
                entity.Property(e => e.KodPocztowy).HasColumnName("kodpocztowy");
                entity.Property(e => e.Poczta).HasColumnName("poczta");
                entity.Property(e => e.Gmina).HasColumnName("gmina");
                entity.Property(e => e.Powiat).HasColumnName("powiat");
                entity.Property(e => e.Wojewodztwo).HasColumnName("wojewodztwo");
                entity.Property(e => e.Kraj).HasColumnName("kraj");

            });

            // Konfiguracja tabeli Koordynaty
            modelBuilder.Entity<Koordynaty>(entity =>
            {
                entity.ToTable("koordynaty");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.NumerKrs).HasColumnName("numerkrs");
                entity.Property(e => e.Latitude).HasColumnName("latitude");
                entity.Property(e => e.Longitude).HasColumnName("longitude");

            });

            // Konfiguracja tabeli Kategoria
            modelBuilder.Entity<Kategoria>(entity =>
            {
                entity.ToTable("kategoria");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Nazwa).HasColumnName("nazwa");
            });

            // Konfiguracja tabeli łączącej OrganizacjaKategoria (bez relacji)
            modelBuilder.Entity<OrganizacjaKategoria>(entity =>
            {
                entity.ToTable("organizacjakategoria");
                entity.HasKey(e => new { e.NumerKrs, e.KategoriaId });
                entity.Property(e => e.NumerKrs).HasColumnName("numerkrs");
                entity.Property(e => e.KategoriaId).HasColumnName("kategoriaid");
            });
        }
    }
}
