using AlgoTrading.Domain.Strategies;
using AlgoTrading.Infrastructure.Strategies;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlgoTrading.Web.Tests.Strategies;

public sealed class JsonFileStrategyRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "algotrading-strategies-" + Guid.NewGuid().ToString("N"));
    private readonly JsonFileStrategyRepository _repository;

    public JsonFileStrategyRepositoryTests() =>
        _repository = new JsonFileStrategyRepository(Options.Create(new StrategyStoreOptions { Directory = _directory }));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task should_write_a_file_the_command_line_reads_back()
    {
        var strategy = Strategy("Bollinger et RSI");

        var key = await _repository.SaveAsync(JsonFileStrategyRepository.KeyFor(strategy.Name), strategy, TestContext.Current.CancellationToken);

        key.ShouldBe("bollinger-et-rsi");
        (await _repository.ListAsync(TestContext.Current.CancellationToken)).ShouldBe(["bollinger-et-rsi"]);
        StrategyDefinition.FromJson((await _repository.ReadAsync(key, TestContext.Current.CancellationToken))!).Fingerprint.ShouldBe(strategy.Fingerprint);
    }

    [Fact]
    public async Task should_refuse_to_save_an_invalid_strategy()
    {
        var invalid = Strategy("Sans règle") with { Entry = new SignalPolicy() };

        await Should.ThrowAsync<InvalidOperationException>(() => _repository.SaveAsync("sans-regle", invalid, TestContext.Current.CancellationToken));
        Directory.Exists(_directory).ShouldBeFalse();
    }

    [Theory]
    [InlineData("../evasion")]
    [InlineData("sous/dossier")]
    [InlineData("")]
    public async Task should_never_let_a_key_escape_the_strategy_directory(string key)
    {
        await Should.ThrowAsync<ArgumentException>(() => _repository.ReadAsync(key, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Bollinger et RSI", "bollinger-et-rsi")]
    [InlineData("Stratégie d'été — MACD", "strategie-d-ete-macd")]
    [InlineData("!!!", "strategie")]
    public void should_derive_a_file_key_from_a_free_name(string name, string expected)
    {
        JsonFileStrategyRepository.KeyFor(name).ShouldBe(expected);
    }

    private static StrategyDefinition Strategy(string name) => new()
    {
        Name = name,
        Entry = new SignalPolicy(rules: [RuleRegistry.DefaultFor("Rsi")]),
    };
}
