using System;
using ConsoleApp1.Indicators;

namespace ConsoleApp1.Strategy;

/// <summary>
/// Aggregates multiple technical indicators to provide consolidated trading signals.
/// This class combines the signals from multiple indicators using different aggregation strategies
/// to reduce false signals and improve trading decision accuracy.
/// 
/// The aggregator supports different modes:
/// - Consensus: All indicators must agree (AND logic)
/// - Majority: More than half of indicators must agree
/// - Weighted: Indicators are weighted based on their reliability
/// - Any: At least one indicator must agree (OR logic)
/// </summary>
public partial class IndicatorAggregator : IIndicator
{
    /// <summary>
    /// List of indicators to aggregate
    /// </summary>
    private readonly IList<IIndicator> indicators;
    
    /// <summary>
    /// Weights for each indicator (used in weighted mode)
    /// </summary>
    private readonly Dictionary<IIndicator, decimal> indicatorWeights;
    
    /// <summary>
    /// Aggregation strategy to use
    /// </summary>
    public AggregationStrategy Strategy { get; }
    
    /// <summary>
    /// Minimum percentage of indicators that must agree (for majority mode)
    /// </summary>
    public decimal MajorityThreshold { get; }

    /// <summary>
    /// Initializes a new instance of the IndicatorAggregator with consensus strategy
    /// </summary>
    /// <param name="indicators">List of indicators to aggregate</param>
    public IndicatorAggregator(IList<IIndicator> indicators) 
        : this(indicators, AggregationStrategy.Consensus, 0.5m)
    {
    }

    /// <summary>
    /// Initializes a new instance of the IndicatorAggregator with custom strategy
    /// </summary>
    /// <param name="indicators">List of indicators to aggregate</param>
    /// <param name="strategy">Aggregation strategy to use</param>
    /// <param name="majorityThreshold">Threshold for majority mode (0.0 to 1.0)</param>
    public IndicatorAggregator(IList<IIndicator> indicators, AggregationStrategy strategy, decimal majorityThreshold = 0.5m)
    {
        // Validate parameters
        if (indicators == null)
            throw new ArgumentNullException(nameof(indicators), "Indicators list cannot be null");
        
        if (indicators.Count == 0)
            throw new ArgumentException("At least one indicator is required", nameof(indicators));
        
        if (majorityThreshold < 0 || majorityThreshold > 1)
            throw new ArgumentException("Majority threshold must be between 0 and 1", nameof(majorityThreshold));

        this.indicators = indicators;
        this.Strategy = strategy;
        this.MajorityThreshold = majorityThreshold;
        this.indicatorWeights = new Dictionary<IIndicator, decimal>();
        
        // Initialize equal weights for weighted mode
        if (strategy == AggregationStrategy.Weighted)
        {
            decimal equalWeight = 1.0m / indicators.Count;
            foreach (var indicator in indicators)
            {
                indicatorWeights[indicator] = equalWeight;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the IndicatorAggregator with weighted strategy
    /// </summary>
    /// <param name="indicators">List of indicators to aggregate</param>
    /// <param name="weights">Dictionary mapping indicators to their weights</param>
    public IndicatorAggregator(IList<IIndicator> indicators, Dictionary<IIndicator, decimal> weights)
    {
        // Validate parameters
        if (indicators == null)
            throw new ArgumentNullException(nameof(indicators), "Indicators list cannot be null");
        
        if (weights == null)
            throw new ArgumentNullException(nameof(weights), "Weights dictionary cannot be null");
        
        if (indicators.Count == 0)
            throw new ArgumentException("At least one indicator is required", nameof(indicators));
        
        if (indicators.Count != weights.Count)
            throw new ArgumentException("Number of indicators must match number of weights", nameof(weights));

        this.indicators = indicators;
        this.Strategy = AggregationStrategy.Weighted;
        this.MajorityThreshold = 0.5m;
        this.indicatorWeights = new Dictionary<IIndicator, decimal>(weights);
        
        // Validate that weights sum to approximately 1.0
        decimal totalWeight = weights.Values.Sum();
        if (Math.Abs(totalWeight - 1.0m) > 0.01m)
        {
            throw new ArgumentException("Weights must sum to 1.0", nameof(weights));
        }
    }

    /// <summary>
    /// Gets the list of indicators being aggregated
    /// </summary>
    public IList<IIndicator> Indicators => indicators;

    /// <summary>
    /// Gets the weights for each indicator (only applicable for weighted strategy)
    /// </summary>
    public IReadOnlyDictionary<IIndicator, decimal> IndicatorWeights => indicatorWeights;

    /// <summary>
    /// Determines if the market is bearish based on aggregated indicator signals
    /// </summary>
    /// <param name="date">The date to check for bearish conditions</param>
    /// <returns>True if aggregated signals indicate bearish conditions, false otherwise</returns>
    public bool IsBearish(DateTime date)
    {
        if (indicators.Count == 0)
            return false;

        return Strategy switch
        {
            AggregationStrategy.Consensus => indicators.All(indicator => indicator.IsBearish(date)),
            AggregationStrategy.Majority => GetBearishCount(date) >= Math.Ceiling(indicators.Count * MajorityThreshold),
            AggregationStrategy.Any => indicators.Any(indicator => indicator.IsBearish(date)),
            AggregationStrategy.Weighted => CalculateWeightedBearishSignal(date),
            _ => throw new InvalidOperationException($"Unknown aggregation strategy: {Strategy}")
        };
    }

    /// <summary>
    /// Determines if the market is bullish based on aggregated indicator signals
    /// </summary>
    /// <param name="date">The date to check for bullish conditions</param>
    /// <returns>True if aggregated signals indicate bullish conditions, false otherwise</returns>
    public bool IsBullish(DateTime date)
    {
        if (indicators.Count == 0)
            return false;

        return Strategy switch
        {
            AggregationStrategy.Consensus => indicators.All(indicator => indicator.IsBullish(date)),
            AggregationStrategy.Majority => GetBullishCount(date) >= Math.Ceiling(indicators.Count * MajorityThreshold),
            AggregationStrategy.Any => indicators.Any(indicator => indicator.IsBullish(date)),
            AggregationStrategy.Weighted => CalculateWeightedBullishSignal(date),
            _ => throw new InvalidOperationException($"Unknown aggregation strategy: {Strategy}")
        };
    }

    /// <summary>
    /// Gets the number of indicators showing bearish signals for the specified date
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>Count of bearish indicators</returns>
    public int GetBearishCount(DateTime date)
    {
        return indicators.Count(indicator => indicator.IsBearish(date));
    }

    /// <summary>
    /// Gets the number of indicators showing bullish signals for the specified date
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>Count of bullish indicators</returns>
    public int GetBullishCount(DateTime date)
    {
        return indicators.Count(indicator => indicator.IsBullish(date));
    }

    /// <summary>
    /// Gets detailed signal information for all indicators
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>Dictionary containing signal details for each indicator</returns>
    public Dictionary<IIndicator, (bool IsBullish, bool IsBearish)> GetDetailedSignals(DateTime date)
    {
        var signals = new Dictionary<IIndicator, (bool IsBullish, bool IsBearish)>();
        
        foreach (var indicator in indicators)
        {
            signals[indicator] = (indicator.IsBullish(date), indicator.IsBearish(date));
        }
        
        return signals;
    }

    /// <summary>
    /// Calculates weighted bearish signal for weighted aggregation strategy
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>True if weighted signal indicates bearish conditions</returns>
    private bool CalculateWeightedBearishSignal(DateTime date)
    {
        decimal weightedSum = 0;
        
        foreach (var indicator in indicators)
        {
            if (indicator.IsBearish(date))
            {
                weightedSum += indicatorWeights[indicator];
            }
        }
        
        // Signal is bearish if weighted sum exceeds 0.5 (majority threshold)
        return weightedSum > 0.5m;
    }

    /// <summary>
    /// Calculates weighted bullish signal for weighted aggregation strategy
    /// </summary>
    /// <param name="date">The date to check</param>
    /// <returns>True if weighted signal indicates bullish conditions</returns>
    private bool CalculateWeightedBullishSignal(DateTime date)
    {
        decimal weightedSum = 0;
        
        foreach (var indicator in indicators)
        {
            if (indicator.IsBullish(date))
            {
                weightedSum += indicatorWeights[indicator];
            }
        }
        
        // Signal is bullish if weighted sum exceeds 0.5 (majority threshold)
        return weightedSum > 0.5m;
    }

    /// <summary>
    /// Sets custom weights for indicators (only applicable for weighted strategy)
    /// </summary>
    /// <param name="weights">Dictionary mapping indicators to their weights</param>
    public void SetWeights(Dictionary<IIndicator, decimal> weights)
    {
        if (Strategy != AggregationStrategy.Weighted)
        {
            throw new InvalidOperationException("Weights can only be set for weighted aggregation strategy");
        }
        
        if (weights == null)
            throw new ArgumentNullException(nameof(weights));
        
        if (weights.Count != indicators.Count)
            throw new ArgumentException("Number of weights must match number of indicators");
        
        decimal totalWeight = weights.Values.Sum();
        if (Math.Abs(totalWeight - 1.0m) > 0.01m)
        {
            throw new ArgumentException("Weights must sum to 1.0");
        }
        
        indicatorWeights.Clear();
        foreach (var kvp in weights)
        {
            indicatorWeights[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Returns a string representation of the indicator aggregator
    /// </summary>
    /// <returns>String containing aggregator configuration and indicator count</returns>
    public override string ToString()
    {
        return string.Join(", ", indicators.Select(i => i.GetType()));
        // return $"IndicatorAggregator({Strategy}) - {indicators.Count} indicators " +
        //        $"(Majority Threshold: {MajorityThreshold:P0})";
    }

    public decimal GetPrice(DateTime date)
    {
        return indicators[0].GetPrice(date); // Assuming all indicators provide the same price
    }
}
