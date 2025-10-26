using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Models.MatchMaker;

namespace ParasolBackEnd.Data
{
    public class SecondDbContext : DbContext
    {
        public SecondDbContext(DbContextOptions<SecondDbContext> options) : base(options)
        {
        }

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostCategory> PostCategories { get; set; }
        public DbSet<PostTag> PostTags { get; set; }
        public DbSet<OrganizationCategory> OrganizationCategories { get; set; }
        public DbSet<OrganizationTag> OrganizationTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguracja tabeli Post
            modelBuilder.Entity<Post>(entity =>
            {
                entity.ToTable("posts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ContactEmail).HasColumnName("contact_email");
                entity.Property(e => e.ContactPhone).HasColumnName("contact_phone");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasConversion<DateOnly>();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasConversion<DateOnly>();
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at").HasConversion<DateOnly?>();
                entity.Property(e => e.OrganizationId).HasColumnName("organization_id");

                // Indeksy dla lepszej wydajności
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_posts_created_at");
                entity.HasIndex(e => e.OrganizationId).HasDatabaseName("ix_posts_organization_id");
                entity.HasIndex(e => e.Status).HasDatabaseName("ix_posts_status");
                entity.HasIndex(e => e.Title).HasDatabaseName("ix_posts_title");
            });

            // Konfiguracja tabeli Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");

                // Indeks na nazwie kategorii
                entity.HasIndex(e => e.Name).HasDatabaseName("ix_categories_name");
            });

            // Konfiguracja tabeli Tag
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.ToTable("tags");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.CategoryId).HasColumnName("category_id");

                // Konfiguracja relacji z Category
                entity.HasOne(t => t.Category)
                    .WithMany(c => c.Tags)
                    .HasForeignKey(t => t.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indeksy
                entity.HasIndex(e => e.CategoryId).HasDatabaseName("ix_tags_category_id");
                entity.HasIndex(e => e.Name).HasDatabaseName("ix_tags_name");
            });

            // Konfiguracja tabeli Organization
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.ToTable("organizations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.OrganizationName).HasColumnName("organization_name");
                entity.Property(e => e.KrsNumber).HasColumnName("krs_number");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.AboutText).HasColumnName("about_text");
                entity.Property(e => e.WebsiteUrl).HasColumnName("website_url");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.ContactEmail).HasColumnName("contact_email");

                // Indeksy
                entity.HasIndex(e => e.Email).HasDatabaseName("ix_organizations_email");
                entity.HasIndex(e => e.OrganizationName).HasDatabaseName("ix_organizations_name");
            });

            // Konfiguracja tabeli PostCategory
            modelBuilder.Entity<PostCategory>(entity =>
            {
                entity.ToTable("post_categories");
                entity.HasKey(e => new { e.PostId, e.CategoryId });
                entity.Property(e => e.PostId).HasColumnName("post_id");
                entity.Property(e => e.CategoryId).HasColumnName("category_id");

                // Indeksy
                entity.HasIndex(e => e.PostId).HasDatabaseName("ix_post_categories_post_id");
                entity.HasIndex(e => e.CategoryId).HasDatabaseName("ix_post_categories_category_id");
            });

            // Konfiguracja tabeli PostTag
            modelBuilder.Entity<PostTag>(entity =>
            {
                entity.ToTable("post_tags");
                entity.HasKey(e => new { e.PostId, e.TagId });
                entity.Property(e => e.PostId).HasColumnName("post_id");
                entity.Property(e => e.TagId).HasColumnName("tag_id");

                // Indeksy
                entity.HasIndex(e => e.PostId).HasDatabaseName("ix_post_tags_post_id");
                entity.HasIndex(e => e.TagId).HasDatabaseName("ix_post_tags_tag_id");
            });

            // Konfiguracja tabeli OrganizationCategory
            modelBuilder.Entity<OrganizationCategory>(entity =>
            {
                entity.ToTable("organization_categories");
                entity.HasKey(e => new { e.OrganizationId, e.CategoryId });
                entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
                entity.Property(e => e.CategoryId).HasColumnName("category_id");

                // Konfiguracja relacji
                entity.HasOne(oc => oc.Organization)
                    .WithMany(o => o.OrganizationCategories)
                    .HasForeignKey(oc => oc.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oc => oc.Category)
                    .WithMany()
                    .HasForeignKey(oc => oc.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indeksy
                entity.HasIndex(e => e.OrganizationId).HasDatabaseName("ix_organization_categories_organization_id");
                entity.HasIndex(e => e.CategoryId).HasDatabaseName("ix_organization_categories_category_id");
            });

            // Konfiguracja tabeli OrganizationTag
            modelBuilder.Entity<OrganizationTag>(entity =>
            {
                entity.ToTable("organization_tags");
                entity.HasKey(e => new { e.OrganizationId, e.TagId });
                entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
                entity.Property(e => e.TagId).HasColumnName("tag_id");

                // Konfiguracja relacji
                entity.HasOne(ot => ot.Organization)
                    .WithMany(o => o.OrganizationTags)
                    .HasForeignKey(ot => ot.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ot => ot.Tag)
                    .WithMany()
                    .HasForeignKey(ot => ot.TagId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indeksy
                entity.HasIndex(e => e.OrganizationId).HasDatabaseName("ix_organization_tags_organization_id");
                entity.HasIndex(e => e.TagId).HasDatabaseName("ix_organization_tags_tag_id");
            });
        }
    }
}
