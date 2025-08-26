using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DebrisRequest> DebrisRequests => Set<DebrisRequest>();
    public DbSet<RequestUpdate> RequestUpdates => Set<RequestUpdate>();
    public DbSet<RoadClosure> RoadClosures => Set<RoadClosure>();
    public DbSet<Zone> Zones => Set<Zone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DebrisRequest
        modelBuilder.Entity<DebrisRequest>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FullName).IsRequired();
            b.Property(x => x.Address).IsRequired();
            // If your entity has CreatedAt with a CLR default, EF will use it; no DB default needed.
        });

        // RequestUpdate (timeline entries)
        modelBuilder.Entity<RequestUpdate>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DebrisRequestId);
            b.HasOne<DebrisRequest>()          // no navigation needed on DebrisRequest
             .WithMany()
             .HasForeignKey(x => x.DebrisRequestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // RoadClosure (live feed mapping)
        modelBuilder.Entity<RoadClosure>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.RoadName).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.HasIndex(x => x.UpdatedAt);
        });

        // Zone (keep minimal; only assume Id exists)
        modelBuilder.Entity<Zone>(b =>
        {
            b.HasKey(x => x.Id);
        });
    }
}
