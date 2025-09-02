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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguracja tabeli Organization
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.ToTable("organizations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.OrganizationName).HasColumnName("organization_name").HasMaxLength(255);
                entity.Property(e => e.KrsNumber).HasColumnName("krs_number").HasMaxLength(20);
                
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Konfiguracja tabeli Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            });

            // Konfiguracja tabeli Tag
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.ToTable("tags");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
                entity.Property(e => e.CategoryId).HasColumnName("category_id");
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Tags)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja tabeli Post
            modelBuilder.Entity<Post>(entity =>
            {
                entity.ToTable("posts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255);
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.OfferDescription).HasColumnName("offer_description");
                entity.Property(e => e.ContactInfo).HasColumnName("contact_info");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
                entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
                
                entity.HasOne(e => e.Organization)
                    .WithMany(o => o.Posts)
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja tabeli PostCategory (many-to-many)
            modelBuilder.Entity<PostCategory>(entity =>
            {
                entity.ToTable("post_categories");
                entity.HasKey(e => new { e.PostId, e.CategoryId });
                entity.Property(e => e.PostId).HasColumnName("post_id");
                entity.Property(e => e.CategoryId).HasColumnName("category_id");
                
                entity.HasOne(e => e.Post)
                    .WithMany(p => p.PostCategories)
                    .HasForeignKey(e => e.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.PostCategories)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Konfiguracja tabeli PostTag (many-to-many)
            modelBuilder.Entity<PostTag>(entity =>
            {
                entity.ToTable("post_tags");
                entity.HasKey(e => new { e.PostId, e.TagId });
                entity.Property(e => e.PostId).HasColumnName("post_id");
                entity.Property(e => e.TagId).HasColumnName("tag_id");
                
                entity.HasOne(e => e.Post)
                    .WithMany(p => p.PostTags)
                    .HasForeignKey(e => e.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Tag)
                    .WithMany(t => t.PostTags)
                    .HasForeignKey(e => e.TagId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
