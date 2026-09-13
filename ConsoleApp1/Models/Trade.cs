public class Trade
{
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public int Quantity { get; set; }
    public decimal ProfitLoss { get; set; }
    public required string StockSymbol { get; set; }
    public bool IsPositive => ProfitLoss > 0;
}