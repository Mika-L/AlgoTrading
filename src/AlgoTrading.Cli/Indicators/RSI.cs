using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Relative Strength Index (RSI) - A momentum oscillator that measures the speed and magnitude of price changes.
/// RSI oscillates between 0 and 100, with values above 70 indicating overbought conditions and values below 30 indicating oversold conditions.
/// 
/// The RSI is calculated using the following steps:
/// 1. Calculate price changes (gains and losses) between consecutive periods
/// 2. Calculate average gains and average losses over the specified period
/// 3. Calculate Relative Strength (RS) = Average Gain / Average Loss
/// 4. Calculate RSI = 100 - (100 / (1 + RS))
/// 
/// Uses Wilder's smoothing method for more accurate calculations.
/// </summary>
public class RSI : BaseIndicator
{
    /// <summary>
    /// Dictionary storing RSI values keyed by date
    /// </summary>
    private readonly Dictionary<DateTime, decimal> rsi;
    
    /// <summary>
    /// Default period for RSI calculation (typically 14 periods)
    /// </summary>
    private const int DefaultPeriod = 14;
    
    /// <summary>
    /// The period used for RSI calculation
    /// </summary>
    public int Period { get; }
    
    /// <summary>
    /// Overbought threshold (default: 70)
    /// </summary>
    public decimal OverboughtThreshold { get; }
    
    /// <summary>
    /// Oversold threshold (default: 30)
    /// </summary>
    public decimal OversoldThreshold { get; }

    /// <summary>
    /// Initializes a new instance of the RSI class with default parameters
    /// </summary>
    /// <param name="prices">List of stock price history data</param>
    public RSI(List<StockPriceHistory> prices) : this(prices, DefaultPeriod, 70, 30)
    {
    }

    /// <summary>
    /// Initializes a new instance of the RSI class with custom parameters
    /// </summary>
    /// <param name="prices">List of stock price history data</param>
    /// <param name="period">Period for RSI calculation (default: 14)</param>
    /// <param name="overboughtThreshold">Overbought threshold (default: 70)</param>
    /// <param name="oversoldThreshold">Oversold threshold (default: 30)</param>
    public RSI(List<StockPriceHistory> prices, int period, decimal overboughtThreshold, decimal oversoldThreshold) : base(prices)
    {
        // Validate parameters
        if (prices == null)
            throw new ArgumentNullException(nameof(prices), "Price data cannot be null");
        
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        
        if (overboughtThreshold <= oversoldThreshold)
            throw new ArgumentException("Overbought threshold must be greater than oversold threshold");
        
        if (overboughtThreshold > 100 || oversoldThreshold < 0)
            throw new ArgumentException("Thresholds must be between 0 and 100");

        this.Period = period;
        this.OverboughtThreshold = overboughtThreshold;
        this.OversoldThreshold = oversoldThreshold;
        
        // Calculate RSI values
        rsi = CalculateRSI(prices);
    }

    /// <summary>
    /// Determines if the market is bearish (overbought) for the specified date
    /// </summary>
    /// <param name="date">The date to check for bearish conditions</param>
    /// <returns>True if RSI indicates overbought conditions (bearish signal), false otherwise</returns>
    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = false;
        if (rsi.TryGetValue(date, out decimal rsiValue))
        {
            // RSI above overbought threshold indicates overbought conditions (bearish signal)
            isBearish = rsiValue > OverboughtThreshold;
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    /// <summary>
    /// Determines if the market is bullish (oversold) for the specified date
    /// </summary>
    /// <param name="date">The date to check for bullish conditions</param>
    /// <returns>True if RSI indicates oversold conditions (bullish signal), false otherwise</returns>
    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = false;
        if (rsi.TryGetValue(date, out decimal rsiValue))
        {
            // RSI below oversold threshold indicates oversold conditions (bullish signal)
            isBullish = rsiValue < OversoldThreshold;
        }
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    /// <summary>
    /// Gets the RSI value for a specific date
    /// </summary>
    /// <param name="date">The date to get RSI value for</param>
    /// <returns>RSI value if available, null otherwise</returns>
    public decimal? GetRSIValue(DateTime date)
    {
        return rsi.TryGetValue(date, out decimal value) ? value : null;
    }

    /// <summary>
    /// Gets all calculated RSI values
    /// </summary>
    /// <returns>Dictionary of RSI values keyed by date</returns>
    public Dictionary<DateTime, decimal> GetAllRSIValues()
    {
        return new Dictionary<DateTime, decimal>(rsi);
    }

    /// <summary>
    /// Gets the latest RSI value
    /// </summary>
    /// <returns>The most recent RSI value, or null if no data available</returns>
    public decimal? GetLatestRSIValue()
    {
        if (rsi.Count == 0)
            return null;
        
        return rsi.Values.Last();
    }

    /// <summary>
    /// Gets the latest RSI date
    /// </summary>
    /// <returns>The date of the most recent RSI calculation, or null if no data available</returns>
    public DateTime? GetLatestRSIDate()
    {
        if (rsi.Count == 0)
            return null;
        
        return rsi.Keys.Last();
    }

    /// <summary>
    /// Calculates RSI values for the given price data using Wilder's smoothing method
    /// </summary>
    /// <param name="prices">List of stock price history data</param>
    /// <returns>Dictionary of RSI values keyed by date</returns>
    private Dictionary<DateTime, decimal> CalculateRSI(List<StockPriceHistory> prices)
    {
        var result = new Dictionary<DateTime, decimal>();
        
        // Validate input data
        if (prices == null || prices.Count < 2)
        {
            //Console.WriteLine("Warning: Insufficient data for RSI calculation. At least 2 price points required.");
            return result;
        }

        // Sort prices by date to ensure chronological order
        var sortedPrices = prices.OrderBy(p => p.Date).ToList();
        
        // Check if we have enough data for the specified period
        if (sortedPrices.Count <= Period)
        {
            //Console.WriteLine($"Warning: Insufficient data for {Period}-period RSI. Need at least {Period + 1} price points.");
            return result;
        }
        
        // Calculate price changes (gains and losses)
        var gains = new List<decimal>();
        var losses = new List<decimal>();
        
        for (int i = 1; i < sortedPrices.Count; i++)
        {
            decimal change = sortedPrices[i].AdjustedClose - sortedPrices[i - 1].AdjustedClose;
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }
        
        // Calculate initial average gain and loss for the first 'Period' periods
        decimal avgGain = gains.Take(Period).Average();
        decimal avgLoss = losses.Take(Period).Average();
        
        // Calculate RSI for the first period after the initial calculation period
        decimal rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
        decimal rsi = 100 - (100 / (1 + rs));
        
        // Store RSI for the first period
        if (sortedPrices.Count > Period)
        {
            result[sortedPrices[Period].Date] = rsi;
        }
        
        // Calculate RSI for remaining periods using Wilder's smoothing method
        for (int i = Period + 1; i < sortedPrices.Count; i++)
        {
            // Use Wilder's smoothing method:
            // New Avg Gain = ((Previous Avg Gain × (Period-1)) + Current Gain) / Period
            // New Avg Loss = ((Previous Avg Loss × (Period-1)) + Current Loss) / Period
            avgGain = ((avgGain * (Period - 1)) + gains[i - 1]) / Period;
            avgLoss = ((avgLoss * (Period - 1)) + losses[i - 1]) / Period;
            
            // Calculate Relative Strength (RS)
            rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
            
            // Calculate RSI using the formula: RSI = 100 - (100 / (1 + RS))
            rsi = 100 - (100 / (1 + rs));
            
            // Store the RSI value for this date
            result[sortedPrices[i].Date] = rsi;
        }
        
        return result;
    }

    /// <summary>
    /// Returns a string representation of the RSI indicator
    /// </summary>
    /// <returns>String containing RSI configuration and latest value</returns>
    public override string ToString()
    {
        var latestValue = GetLatestRSIValue();
        var latestDate = GetLatestRSIDate();
        
        return $"RSI({Period}) - Latest: {latestValue:F2} on {latestDate:yyyy-MM-dd} " +
               $"(Overbought: {OverboughtThreshold}, Oversold: {OversoldThreshold})";
    }
}