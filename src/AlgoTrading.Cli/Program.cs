using AlgoTrading.Cli;
using Microsoft.Extensions.DependencyInjection;

// Les options qui décident de la configuration sont lues avant l'hôte ; l'analyse complète
// de la ligne de commande revient ensuite à System.CommandLine.
var bootstrap = BootstrapOptions.Parse(args);

using var host = HostFactory.Build(bootstrap);
using var scope = host.Services.CreateScope();

return await CommandTree.Build(scope.ServiceProvider).Parse(args).InvokeAsync().ConfigureAwait(false);
