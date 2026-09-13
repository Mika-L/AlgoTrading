using Microsoft.EntityFrameworkCore;
using ConsoleApp1.Models;

public class TradingContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Stock> Stocks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=stocks.db");
    }
}