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
        public DbSet<AuditEntry> AuditLog { get; set; } = null!;
        public DbSet<AppealSupport> AppealSupports { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(u => u.Acab)
                .WithMany()
                .HasForeignKey(u => u.AcabId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Acab>().HasIndex(a => a.ResidentCode).IsUnique();
            modelBuilder.Entity<Acab>().HasIndex(a => a.HeadCode).IsUnique();
            modelBuilder.Entity<Acab>().Property(a => a.ResidentCode).HasMaxLength(32);
            modelBuilder.Entity<Acab>().Property(a => a.HeadCode).HasMaxLength(32);

            var appeal = modelBuilder.Entity<Appeal>();

            appeal.Property(a => a.Location)
                .HasColumnType("geography (point, 4326)")
                .HasComputedColumnSql("ST_SetSRID(ST_MakePoint(\"Lng\"::double precision, \"Lat\"::double precision), 4326)::geography", stored: true);
            appeal.HasIndex(a => a.Location).HasMethod("gist");

            appeal.Property(a => a.Lat).HasPrecision(8, 5);
            appeal.Property(a => a.Lng).HasPrecision(8, 5);

            appeal.HasIndex(a => a.CreatedAt);
            appeal.HasIndex(a => new { a.AcabId, a.Status });

            appeal.HasOne(a => a.Author).WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Acab).WithMany().HasForeignKey(a => a.AcabId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(a => a.Recipient).WithMany().HasForeignKey(a => a.RecipientId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppealUpdate>()
                .HasOne(u => u.Appeal)
                .WithMany(a => a.Updates)
                .HasForeignKey(u => u.AppealId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.EmailState, n.SmsState });

            modelBuilder.Entity<AppealSupport>().HasKey(s => new { s.AppealId, s.UserId });
            modelBuilder.Entity<AppealSupport>()
                .HasOne(s => s.Appeal).WithMany(a => a.Supports).HasForeignKey(s => s.AppealId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AppealSupport>()
                .HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

            appeal.HasIndex(a => a.DueAt);
            modelBuilder.Entity<User>().HasIndex(u => u.TelegramLinkCode).IsUnique();

            modelBuilder.Entity<AuditEntry>().ToTable("AuditLog");
            modelBuilder.Entity<AuditEntry>().HasIndex(e => e.At);
        }
    }
}
