using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConsoleApp1.Models;

public class StockPriceHistory
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StockId { get; set; }

    [ForeignKey(nameof(StockId))]
    public virtual Stock? Stock { get; set; }

    [Column(TypeName = "date")]
    public DateTime Date { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Close { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Open { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal High { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Low { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdjustedClose { get; set; }
    public decimal AdjustedOpen => Close != 0 ? Open * AdjustedClose / Close : 0m;
    public decimal AdjustedHigh => Close != 0 ? High * AdjustedClose / Close : 0m;
    public decimal AdjustedLow => Close != 0 ? Low * AdjustedClose / Close : 0m;

    [Column(TypeName = "bigint")]
    public long Volume { get; set; }

    // [Column(TypeName = "decimal(5,2)")]
    // public decimal ChangePercent { get; set; }
    public decimal ChangePercent
    {
        get
        {
            return (Close - Open) / Open * 100;
        }
    }

    /*  [NotMapped]
      public decimal Ema9 { get; internal set; }

      [NotMapped]
      public decimal Ema20 { get; internal set; }

      [NotMapped]
      public decimal Ema50 { get; internal set; }

      [NotMapped]
      public decimal Ema200 { get; internal set; }

      [NotMapped]
      public string EmaSupportResistance50
      {
          get
          {
              string label = Price > Ema50 ? "Support_50" : "Resistance_50";
              return $"{label}:{Ema50}";
          }
      }

      [NotMapped]
      public string EmaSupportResistance200
      {
          get
          {
              string label = Price > Ema200 ? "Support_200" : "Resistance_200";
              return $"{label}:{Ema200}";
          }
      } */

    public override string ToString()
    {
        return $"Id: {Id}, StockId: {StockId}, Date: {Date:yyyy-MM-dd}, " +
               $"Open: {Open}, High: {High}, Low: {Low}, Close: {Close}, " +
               $"AdjustedClose: {AdjustedClose}, Volume: {Volume}, " +
               $"ChangePercent: {ChangePercent:F2}%";
    }
}
