using AlgoTrading.Web.Forms;
using Shouldly;

namespace AlgoTrading.Web.Tests.Forms;

public sealed class RuleCatalogLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "algotrading-rules-" + Guid.NewGuid().ToString("N"));
    private readonly RuleCatalogLibrary _library;

    public RuleCatalogLibraryTests()
    {
        Directory.CreateDirectory(_directory);
        _library = new RuleCatalogLibrary(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void should_list_the_catalog_files_of_the_command_line_by_name()
    {
        File.WriteAllText(Path.Combine(_directory, "screening.json"), "[]");
        File.WriteAllText(Path.Combine(_directory, "catalog.json"), "[]");
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), string.Empty);

        _library.List().ShouldBe(["catalog", "screening"]);
    }

    [Fact]
    public void should_expand_the_ranges_of_a_catalog_file()
    {
        File.WriteAllText(Path.Combine(_directory, "grid.json"), """
            [{ "type": "Sign", "indicator": "Momentum", "parameters": { "period": [5, 10, 20] } }]
            """);

        _library.Read("grid").Rules.Count.ShouldBe(3);
    }

    [Fact]
    public void should_offer_the_twelve_directional_indicators_by_default()
    {
        _library.Read(RuleCatalogLibrary.Default).Rules.Count.ShouldBe(12);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("../appsettings")]
    public void should_read_nothing_but_a_listed_catalog(string name)
    {
        Should.Throw<ArgumentException>(() => _library.Read(name)).Message.ShouldContain("Catalogue inconnu");
    }

    [Fact]
    public void should_list_nothing_when_the_folder_does_not_exist()
    {
        new RuleCatalogLibrary(Path.Combine(_directory, "absent")).List().ShouldBeEmpty();
    }
}
