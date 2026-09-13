using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace AlgoTrading.Architecture.Tests.Boundaries;

/// <summary>
/// Les frontières entre modules d'un même assembly ne sont pas tenues par le compilateur —
/// <c>internal</c> ne sépare rien à l'intérieur d'un assembly. Ces règles sont la seule
/// barrière, et elle est de niveau CI.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly Domain = typeof(AlgoTrading.Domain.MarketData.Symbol).Assembly;

    [Fact]
    public void should_keep_the_domain_free_of_the_file_system_of_ef_and_of_http()
    {
        var result = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "System.IO", "System.Net.Http", "ScottPlot", "CsvHelper")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void should_keep_market_data_independent_of_every_other_module()
    {
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace("AlgoTrading.Domain.MarketData")
            .Should()
            .NotHaveDependencyOnAny(
                "AlgoTrading.Domain.Indicators",
                "AlgoTrading.Domain.Strategies",
                "AlgoTrading.Domain.Backtesting",
                "AlgoTrading.Domain.Reporting")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void should_keep_indicators_from_depending_on_strategies_or_on_the_engine()
    {
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace("AlgoTrading.Domain.Indicators")
            .Should()
            .NotHaveDependencyOnAny(
                "AlgoTrading.Domain.Strategies",
                "AlgoTrading.Domain.Backtesting",
                "AlgoTrading.Domain.Reporting")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }
}
