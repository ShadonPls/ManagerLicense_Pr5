using Microsoft.EntityFrameworkCore;
using Server.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Data
{
    public class Context : DbContext
    {
        public static readonly string ConnectionString = "Host=localhost;Port=5432;Database=pr5;Username=postgres;Password=123;";
        public DbSet<Client> Clients { get; set; }
        public Context()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(ConnectionString);
        }
    }
}
