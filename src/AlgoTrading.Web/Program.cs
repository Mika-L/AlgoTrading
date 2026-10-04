using System.Globalization;
using AlgoTrading.Infrastructure;
using AlgoTrading.Infrastructure.Persistence;
using AlgoTrading.Infrastructure.Providers;
using AlgoTrading.Infrastructure.Strategies;
using AlgoTrading.Web.Components;
using AlgoTrading.Web.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Json;

var builder = WebApplication.CreateBuilder(args);

// L'interface travaille sur la configuration, la base et les stratégies de la ligne de
// commande : univers et exclusions ne se déclarent qu'une fois, et un run lancé d'un côté
// se consulte de l'autre. Les chemins relatifs de cette configuration partent du dossier
// de la ligne de commande, comme lorsqu'on la lance par « dotnet run ».
var cliDirectory = Path.GetFullPath(builder.Configuration["Cli:Directory"] ?? "../AlgoTrading.Cli", builder.Environment.ContentRootPath);

var cliSettings = new JsonConfigurationSource
{
    Path = Path.Combine(cliDirectory, "appsettings.json"),
    Optional = false,
    ReloadOnChange = false,
};
cliSettings.ResolveFileProvider();

// En tête de liste : la configuration propre à l'interface et l'environnement priment.
builder.Configuration.Sources.Insert(0, cliSettings);

string FromCli(string path) => Path.GetFullPath(path, cliDirectory);

var database = FromCli(builder.Configuration.GetConnectionString("Database") ?? "algotrading.db");

builder.Services.AddAlgoTrading(builder.Configuration, database);
builder.Services.PostConfigure<StrategyStoreOptions>(o => o.Directory = FromCli(o.Directory));
builder.Services.PostConfigure<CsvProviderOptions>(o => o.Directory = FromCli(o.Directory));
builder.Services.AddSingleton(IndicatorLineCatalog.Discover());

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Les montants, dates et pourcentages s'affichent à la française, quelle que soit la machine.
var french = CultureInfo.GetCultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = french;
CultureInfo.DefaultThreadCurrentUICulture = french;

var app = builder.Build();

// Même schéma que « algo db migrate » : une base neuve est utilisable sans passer par la CLI.
await using (var context = await app.Services.GetRequiredService<IDbContextFactory<AlgoTradingDbContext>>().CreateDbContextAsync())
{
    await context.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
