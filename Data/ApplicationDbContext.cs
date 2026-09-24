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
    }
}