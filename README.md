# AlgoTrading

Backtester de stratégies d'investissement fondées sur un ou plusieurs indicateurs techniques.
Les stratégies se décrivent dans des fichiers JSON, s'exécutent en ligne de commande, et leurs
résultats sont persistés pour être comparés d'un run à l'autre.

## Démarrage

```bash
dotnet build
dotnet run --project src/AlgoTrading.Cli -- db migrate
dotnet run --project src/AlgoTrading.Cli -- data fetch --universe cac40 --from 2017-01-01
dotnet run --project src/AlgoTrading.Cli -- backtest run --strategy strategies/bollinger-rsi.json --save
```

## Commandes

| Commande | Rôle |
| --- | --- |
| `algo db migrate` | Crée ou met à jour le schéma. |
| `algo db import-legacy --source <fichier>` | Reprend l'historique d'une ancienne base (transition). |
| `algo data fetch --universe <nom> --from <date>` | Télécharge un univers (`--provider yahoo\|csv`). |
| `algo data list` | Instruments connus et étendue de leur historique. |
| `algo data gaps` | Séances manquantes et **ruptures de cours**. |
| `algo backtest run --strategy <fichier>` | Exécute une stratégie (`--save`, `--from`, `--to`, `--cash`). |
| `algo backtest optimize --rules <fichier>` | Explore les combinaisons de règles (`--min-k`, `--max-k`, `--top`). |
| `algo backtest compare --runs 12,17` | Compare des résultats enregistrés. |
| `algo report show --run <n>` | Affiche un résultat. |
| `algo report chart --run <n>` | Rejoue un run et exporte CSV et graphique. |

Options globales : `--config <fichier>`, `--db <chemin>`, `--verbosity quiet|error|warning|info|debug|trace`.

La journalisation de diagnostic et la sortie utilisateur sont séparées : `--verbosity quiet`
fait taire les traces sans masquer les résultats.

## Architecture

```
src/
  AlgoTrading.Domain/          aucune référence projet, aucun paquet d'infrastructure
    MarketData/                Symbol, PriceBar, BarSeries, TradingCalendar
    Indicators/                le noyau Series et les 13 indicateurs
    Strategies/                Signal, les 6 règles, l'agrégation, StrategyDefinition
    Backtesting/               le moteur, le portefeuille, le registre de trades, l'optimiseur
    Reporting/                 mesures de performance et résultat de backtest
  AlgoTrading.Application/     cas d'usage et ports
  AlgoTrading.Infrastructure/  EF Core / SQLite, Yahoo, CSV, ScottPlot
  AlgoTrading.Cli/             racine de composition et ligne de commande
tests/
  AlgoTrading.Domain.Tests/       indicateurs, règles, moteur, mesures
  AlgoTrading.Architecture.Tests/ frontières entre modules
```

Dépendances : `Domain` ← `Application` ← `Infrastructure` ← `Cli`. Le domaine ne référence
aucun projet.

**Ce qui tient les frontières.** Le domaine n'a aucun paquet d'infrastructure :
`using Microsoft.EntityFrameworkCore;` n'y compile pas. S'y ajoute un `BannedSymbols.txt` qui
fait de `Console`, `DateTime.Now`, `File`, `Random` et `Environment` des **erreurs** de
compilation dans le domaine. Entre modules d'un même assembly, `internal` ne sépare rien : la
seule barrière est `AlgoTrading.Architecture.Tests`, et elle est de niveau CI.

## Conventions

- **Tout taux est une fraction.** `0,02` vaut 2 %. La multiplication par cent n'existe que
  dans le formatage de la ligne de commande.
- **Les dates sont des `DateOnly`.** Aucune composante horaire n'existe dans le domaine.
- **Les barres sont rétro-ajustées au chargement.** Le facteur `clôture ajustée / clôture`
  s'applique aux quatre prix ; le domaine ne voit qu'une seule échelle de prix.
- **Les décalages s'expriment en barres, jamais en jours calendaires.** Un croisement du
  vendredi reste détecté quand la barre suivante est un lundi.
- **Le moteur est pur.** Il ne connaît ni base ni fichier : il retourne un résultat que
  l'appelant persiste.

## Écrire une stratégie

```json
{
  "name": "Bollinger et RSI",
  "entry": {
    "mode": "Majority",
    "threshold": 0.5,
    "rules": [
      { "type": "BandBreakout", "indicator": "Bollinger", "parameters": { "period": 20, "multiplier": 2 }, "meanReverting": true },
      { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": 14 }, "bullishBelow": 30, "bearishAbove": 70 }
    ]
  },
  "sizing": { "mode": "EquityFraction", "value": 0.1 }
}
```

Modes d'agrégation : `Consensus`, `Majority`, `Any`, `Weighted`. Le seuil est franchi
**strictement** : deux voix sur quatre ne font pas une majorité.

Types de règles : `Threshold`, `Crossover`, `BandBreakout`, `PriceVsLevel`, `Sign`,
`IchimokuCloud`. Ajouter `"asEvent": true` convertit une règle d'état en règle d'événement —
le signal n'est alors émis qu'au basculement.

Indicateurs : `Ema`, `Macd`, `Momentum`, `Rsi`, `Cci`, `Stochastic`, `WilliamsR`, `Atr`,
`Bollinger`, `Keltner`, `Ichimoku`, `Sar`, `Vwap`.

Ce qui n'est pas écrit prend sa valeur par défaut : frais à 10 points de base avec un minimum
d'un euro, glissement à 5 points de base, dimensionnement à 10 % de la valeur du portefeuille,
aucun stop de protection.

## Qualité des données

`algo data gaps` signale deux choses : les séances manquantes, et les **ruptures de cours** —
une variation trop forte pour être un mouvement de marché, donc une opération sur titre que la
source n'a pas répercutée. Laissée passer, elle fabrique une plus-value fictive : sur l'univers
CAC 40 de 2017 à 2025, deux titres dans ce cas suffisaient à faire passer un rendement de 50 %
pour 214 %.

Ces titres s'écartent par `Universes:Exclusions` dans la configuration — jamais par une
condition dans le code.

## Réserves méthodologiques

Chaque résultat porte ses réserves, affichées avec les chiffres :

- **Biais du survivant.** Appliquer la composition d'un indice d'aujourd'hui au passé surestime
  la performance. C'est le principal défaut méthodologique restant ; le corriger demande un
  univers daté.
- **Fenêtre unique.** Le classement de l'optimiseur se fait par Calmar et non par performance
  brute, mais optimiser sur une seule période reste du surapprentissage. `OptimizationRequest`
  porte déjà `From` et `To` pour qu'un walk-forward soit une boucle, pas une refonte.

## Vérification

```bash
dotnet build && dotnet test
```

Les indicateurs sont éprouvés à trois niveaux : un test anti-look-ahead appliqué au catalogue
entier — *aucun indicateur ne dépend d'une barre postérieure à la date qu'il valorise* —, des
micro-séries calculables de tête, et une comparaison différentielle contre
`Skender.Stock.Indicators`, référencé en test uniquement.
