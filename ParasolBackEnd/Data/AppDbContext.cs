using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Models;

namespace ParasolBackEnd.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Organizacja> Organizacje { get; set; }
        public DbSet<Adres> Adresy { get; set; }
        public DbSet<Koordynaty> Koordynaty { get; set; }
        public DbSet<Kategoria> Kategorie { get; set; }
        public DbSet<OrganizacjaKategoria> OrganizacjaKategorie { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguracja kluczy
            modelBuilder.Entity<Organizacja>()
                .HasKey(o => o.NumerKRS);

            modelBuilder.Entity<Adres>()
                .HasKey(a => new { a.Id, a.NumerKRS });

            modelBuilder.Entity<Koordynaty>()
                .HasKey(k => new { k.Id, k.NumerKRS });

            modelBuilder.Entity<OrganizacjaKategoria>()
                .HasKey(ok => new { ok.NumerKRS, ok.KategoriaId });

            // Relacje
            modelBuilder.Entity<Organizacja>()
                .HasMany(o => o.Adresy)
                .WithOne()
                .HasForeignKey(a => a.NumerKRS);

            modelBuilder.Entity<Organizacja>()
                .HasMany(o => o.Koordynaty)
                .WithOne()
                .HasForeignKey(k => k.NumerKRS);

            modelBuilder.Entity<Organizacja>()
                .HasMany(o => o.OrganizacjaKategorie)
                .WithOne(ok => ok.Organizacja)
                .HasForeignKey(ok => ok.NumerKRS);

            modelBuilder.Entity<Kategoria>()
                .HasMany(k => k.OrganizacjaKategorie)
                .WithOne(ok => ok.Kategoria)
                .HasForeignKey(ok => ok.KategoriaId);
        }
    }
}
