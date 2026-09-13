namespace ConsoleApp1.Strategy;

public partial class IndicatorAggregator
{
    /// <summary>
    /// Available aggregation strategies
    /// </summary>
    public enum AggregationStrategy
    {
        /// <summary>
        /// All indicators must agree (AND logic) - Most conservative
        /// </summary>
        Consensus,
        
        /// <summary>
        /// More than half of indicators must agree
        /// </summary>
        Majority,
        
        /// <summary>
        /// At least one indicator must agree (OR logic) - Most aggressive
        /// </summary>
        Any,
        
        /// <summary>
        /// Weighted average based on indicator reliability
        /// </summary>
        Weighted
    }
}
