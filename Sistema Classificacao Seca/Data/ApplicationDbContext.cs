

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Model;

namespace Sistema_Classificacao_Seca.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Region> Regions { get; set; } = null!;

        public DbSet<SoilObservation> SoilObservations
        { get; set; } = null!;
    }
}
