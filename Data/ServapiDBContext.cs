using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminPortal.Data;
using AdminPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace SERVAPI.Models
{
    public class ServapiDBContext : DbContext
    {
        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientMetric> ClientMetrics { get; set; }
        public DbSet<ClientTask> ClientTasks { get; set; }
        public DbSet<DataItem> DataItems { get; set; }
        public DbSet<ClientLog> ClientLogs { get; set; }
        public string mytestvar;
        public ServapiDBContext()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(FilePath.connection_string);
        }
    }

}
