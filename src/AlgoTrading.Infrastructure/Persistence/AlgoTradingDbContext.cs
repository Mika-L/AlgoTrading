using Microsoft.EntityFrameworkCore;

namespace AlgoTrading.Infrastructure.Persistence;

/// <summary>
/// Le contexte unique, qui remplace les deux contextes d'origine — l'un pour les cotations,
/// l'autre pour les ordres — dont les modèles divergeaient.
/// <para>Il reçoit ses options par constructeur : plus de chaîne de connexion relative
/// codée dans <c>OnConfiguring</c>, dont la résolution dépendait du répertoire d'exécution
/// et faisait travailler l'application sur une base différente selon la façon de la lancer.</para>
/// </summary>
public sealed class AlgoTradingDbContext(DbContextOptions<AlgoTradingDbContext> options) : DbContext(options)
{
    public DbSet<InstrumentRow> Instruments => Set<InstrumentRow>();

    public DbSet<DailyBarRow> Bars => Set<DailyBarRow>();

    public DbSet<BacktestRunRow> Runs => Set<BacktestRunRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<InstrumentRow>(entity =>
        {
            entity.ToTable("Instruments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Symbol).IsUnique();
        });

        modelBuilder.Entity<DailyBarRow>(entity =>
        {
            entity.ToTable("DailyBars");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Instrument)
                .WithMany(i => i.Bars)
                .HasForeignKey(e => e.InstrumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // L'index unique qui manquait : relancer l'initialisation dupliquait purement et
            // simplement tout l'historique.
            entity.HasIndex(e => new { e.InstrumentId, e.Date }).IsUnique();
        });

        modelBuilder.Entity<BacktestRunRow>(entity =>
        {
            entity.ToTable("BacktestRuns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StrategyName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Fingerprint).IsRequired().HasMaxLength(64);
            entity.Property(e => e.StrategyJson).IsRequired();
            entity.Property(e => e.Universe).IsRequired();
            entity.HasIndex(e => e.Fingerprint);
            entity.HasIndex(e => e.RanAt);
        });
    }
}
