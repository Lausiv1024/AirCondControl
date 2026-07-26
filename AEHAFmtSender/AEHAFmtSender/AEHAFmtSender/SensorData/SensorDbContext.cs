using Microsoft.EntityFrameworkCore;

namespace AEHAFmtSender.SensorData
{
    public class SensorDbContext : DbContext
    {
        public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

        public SensorDbContext(DbContextOptions<SensorDbContext> options) : base(options) { }
    }
}
