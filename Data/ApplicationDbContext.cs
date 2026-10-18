using Microsoft.EntityFrameworkCore;
using CityFix_backend.Models;

namespace CityFix_backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Acab> Acabs { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Recipient> Recipients { get; set; } = null!;
        public DbSet<Appeal> Appeals { get; set; } = null!;
        public DbSet<AppealUpdate> AppealUpdates { get; set; } = null!;
        public DbSet<AppealFile> AppealFiles { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Spatial types and ST_* functions for the similar-appeals search.
            modelBuilder.HasPostgresExtension("postgis");

            // Email is the login name, stored lower-cased.
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // A user's association cannot be deleted while it has members.
            modelBuilder.Entity<User>()
                .HasOne(u => u.Acab)
                .WithMany()
                .HasForeignKey(u => u.AcabId)
                .OnDelete(DeleteBehavior.Restrict);

            // Both invitation codes are looked up at registration, so each must point to one association.
            modelBuilder.Entity<Acab>().HasIndex(a => a.ResidentCode).IsUnique();
            modelBuilder.Entity<Acab>().HasIndex(a => a.HeadCode).IsUnique();
            modelBuilder.Entity<Acab>().Property(a => a.ResidentCode).HasMaxLength(32);
            modelBuilder.Entity<Acab>().Property(a => a.HeadCode).HasMaxLength(32);

            var appeal = modelBuilder.Entity<Appeal>();

            // Computed by the database from Lat/Lng; a GiST index makes "within 500 m" an index scan.
            appeal.Property(a => a.Location)
                .HasColumnType("geography (point, 4326)")
                .HasComputedColumnSql("ST_SetSRID(ST_MakePoint(\"Lng\"::double precision, \"Lat\"::double precision), 4326)::geography", stored: true);
            appeal.HasIndex(a => a.Location).HasMethod("gist");

            appeal.Property(a => a.Lat).HasPrecision(8, 5);
            appeal.Property(a => a.Lng).HasPrecision(8, 5);

            // Lists sort and filter by these.
            appeal.HasIndex(a => a.CreatedAt);
            appeal.HasIndex(a => new { a.AcabId, a.Status });

            // Records referenced by appeals cannot be deleted (the controllers explain why first).
            appeal.HasOne(a => a.Author).WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Acab).WithMany().HasForeignKey(a => a.AcabId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Recipient).WithMany().HasForeignKey(a => a.RecipientId).OnDelete(DeleteBehavior.Restrict);

            // History, photos and notifications belong to their appeal and go with it.
            modelBuilder.Entity<AppealUpdate>()
                .HasOne(u => u.Appeal)
                .WithMany(a => a.Updates)
                .HasForeignKey(u => u.AppealId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user who changed a status cannot be deleted silently: that would rewrite the history.
            modelBuilder.Entity<AppealUpdate>()
                .HasOne(u => u.ChangedBy)
                .WithMany()
                .HasForeignKey(u => u.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppealFile>()
                .HasOne(f => f.Appeal)
                .WithMany(a => a.Files)
                .HasForeignKey(f => f.AppealId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Appeal)
                .WithMany()
                .HasForeignKey(n => n.AppealId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead });
        }
    }
}
