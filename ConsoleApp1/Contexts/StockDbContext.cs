using Microsoft.EntityFrameworkCore;
using ConsoleApp1.Models;

public class StockDbContext : DbContext
{
    public DbSet<StockPriceHistory> StockPriceHistories { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=stocks.db");
    }
}