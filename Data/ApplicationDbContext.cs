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

        public DbSet<User> Users { get; set; }
        public DbSet<Acab> Acabs { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Recipient> Recipients { get; set; }
        public DbSet<Appeal> Appeals { get; set; }
        public DbSet<AppealUpdate> AppealUpdates { get; set; }
        public DbSet<AppealFile> AppealFiles { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Email is the login name. Emails are stored lower-cased by AuthService/UsersController.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // [Required] on Appeal.RecipientId is a form rule only; the column stays nullable.
            modelBuilder.Entity<Appeal>()
                .Property(a => a.RecipientId)
                .IsRequired(false);

            // Deleting a recipient leaves its appeals unassigned instead of deleting them.
            modelBuilder.Entity<Appeal>()
                .HasOne(a => a.Recipient)
                .WithMany()
                .HasForeignKey(a => a.RecipientId)
                .OnDelete(DeleteBehavior.SetNull);

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