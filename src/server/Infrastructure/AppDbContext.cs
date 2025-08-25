using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<RoadClosure> RoadClosures => Set<RoadClosure>();
    public DbSet<DebrisRequest> DebrisRequests => Set<DebrisRequest>();
    public DbSet<RequestUpdate> RequestUpdates => Set<RequestUpdate>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<DebrisRequest>().HasIndex(r => new { r.ZoneId, r.Status, r.Priority });
        mb.Entity<RoadClosure>().HasIndex(r => new { r.Status, r.UpdatedAt });
        base.OnModelCreating(mb);
    }
}
