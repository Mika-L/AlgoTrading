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

## Interface web

```bash
dotnet run --project src/AlgoTrading.Web    # http://localhost:5080
```

Une interface Blazor (rendu serveur) pour éditer les stratégies, lancer un backtest et en lire
le résultat : mesures, courbe de valeur, drawdown, relevé de trades filtrable, comparaison de
plusieurs runs en base 100. Elle travaille sur **la même configuration, la même base et les
mêmes fichiers de stratégie** que la ligne de commande — ceux de `src/AlgoTrading.Cli` — si
bien qu'un run lancé d'un côté se consulte de l'autre. La page Données télécharge un univers
et signale les ruptures de cours.

Seuls les chiffres de synthèse d'un run sont persistés : sa page le **rejoue** depuis la
stratégie, la période, le capital et les titres enregistrés, et signale tout écart avec le
résultat d'origine. Dans l'éditeur, les taux se saisissent en pour cent et redeviennent des
fractions à l'enregistrement, sans changer l'empreinte d'une stratégie qu'on n'a pas modifiée.

## Commandes

| Commande | Rôle |
| --- | --- |
| `algo db migrate` | Crée ou met à jour le schéma. |
| `algo db import-legacy --source <fichier>` | Reprend l'historique d'une ancienne base (transition). |
| `algo data fetch --universe <nom> --from <date>` | Télécharge un univers (`--provider yahoo\|csv`). |
| `algo data list` | Instruments connus et étendue de leur historique. |
| `algo data gaps` | Séances manquantes et **ruptures de cours**. |
| `algo backtest run --strategy <fichier>` | Exécute une stratégie (`--save`, `--from`, `--to`, `--cash`). |
| `algo backtest optimize --rules <fichier>` | Explore les combinaisons de règles (`--min-k`, `--max-k`, `--top`, `--min-trades`, `--sample`, `--seed`). |
| `algo backtest walk-forward --rules <fichier>` | Optimise sur une fenêtre, juge le gagnant sur la suivante (`--train-months`, `--test-months`, `--to`). |
| `algo backtest compare --runs 12,17` | Compare des résultats enregistrés. |
| `algo report show --run <n>` | Affiche un résultat. |
| `algo report chart --run <n>` | Rejoue un run et exporte CSV et graphique. |

Options globales : `--config <fichier>`, `--db <chemin>`, `--verbosity quiet|error|warning|info|debug|trace`.

La journalisation de diagnostic et la sortie utilisateur sont séparées : `--verbosity quiet`
fait taire les traces sans masquer les résultats.

## Explorer des stratégies

Dans un catalogue de règles, toute valeur peut être remplacée par une liste ou une plage, et
chaque entrée se développe en autant de variantes que le produit de ses axes :

```json
{
  "type": "Threshold",
  "indicator": "Rsi",
  "parameters": { "period": { "from": 7, "to": 28, "step": 7 } },
  "bullishBelow": [20, 25, 30, 35],
  "bearishAbove": [65, 70, 75, 80]
}
```

Les variantes qu'une règle refuse (un seuil de survente au-dessus du seuil de surachat) sont
écartées et comptées. `rules/screening.json` décline ainsi les douze indicateurs en 152 règles.

L'optimiseur travaille en flux : combinaisons générées à la volée, résultats réduits à leurs
mesures, classement borné à `--top`. La mémoire ne dépend plus du nombre d'essais. Il combine
au plus une variante par indicateur (`--allow-same-indicator` pour lever la contrainte), ne
classe que les combinaisons d'au moins `--min-trades` trades (20 par défaut), et `--sample <n>`
tire `n` combinaisons uniformément, sans remise et de façon reproductible (`--seed`), au lieu de
tout parcourir. Une combinaison coûte environ 0,25 s de CPU sur le CAC 40 complet.

```bash
# Criblage : chaque variante seule, pour repérer les plateaux de paramètres
algo backtest optimize --rules rules/screening.json --min-k 1 --max-k 1 --top 50
# Combinaison : 20 000 tirages parmi les combinaisons de 2 à 4 règles
algo backtest optimize --rules rules/screening.json --sample 20000
```

Le rapport donne le nombre de combinaisons réellement essayées. Il faudra en tenir compte pour
corriger le meilleur score de la chance accumulée au fil des essais.

Le classement d'une exploration ne vaut que sur la période explorée. `walk-forward` optimise
sur une fenêtre (36 mois par défaut), joue le gagnant sur les 12 mois suivants, décale d'un an
et recommence. Les fenêtres de test, mises bout à bout, donnent la seule performance qui
compte : celle de stratégies jugées sur des séances qu'elles n'ont jamais vues. Elle est
présentée face à l'achat-conservation de l'univers à parts égales sur les mêmes fenêtres, sans
frais : une stratégie qui ne bat pas cette référence n'apporte rien qu'un fonds indiciel ne
donne déjà. L'efficacité rapporte le rendement annualisé en test à celui de l'apprentissage. Une période réservée au
verdict final se protège avec `--to` :

```bash
algo backtest walk-forward --rules rules/screening.json --sample 3000 --to 2023-12-31
```

Premier passage sur le CAC 40 (2017-2023, quatre fenêtres de test) : des Calmar
d'apprentissage entre 1,1 et 1,7 tombent à un rendement cumulé de −0,55 % hors échantillon,
quand l'achat-conservation fait +46,65 % pour la même pire baisse (36 %). C'est le
surapprentissage que cette validation sert à révéler.

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
  AlgoTrading.Web/             racine de composition et interface Blazor
tests/
  AlgoTrading.Domain.Tests/       indicateurs, règles, moteur, mesures
  AlgoTrading.Architecture.Tests/ frontières entre modules
  AlgoTrading.Web.Tests/          formulaire de stratégie, bibliothèque de fichiers
```

Dépendances : `Domain` ← `Application` ← `Infrastructure` ← `Cli` et `Web`. Le câblage commun aux
deux points d'entrée est `AddAlgoTrading`, dans l'infrastructure. Le domaine ne référence
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

## Protéger une position

```json
"risk": {
  "stopLoss": 0.05,
  "takeProfit": 0.15,
  "trailingRate": 0.1,
  "trailingAtr": { "multiple": 3, "period": 14 }
}
```

`stopLoss` et `takeProfit` se mesurent au prix de revient. Les deux stops suiveurs se mesurent
au **plus haut atteint depuis l'entrée** et **ne redescendent jamais** : `trailingRate` à
distance constante — `0.1` le tient dix pour cent sous ce plus haut —, `trailingAtr` à
`multiple` fois l'ATR, c'est-à-dire à une distance qui suit l'amplitude propre du titre. Sur le
CAC 40, trois ATR valent 4 % sur une valeur tranquille et 8 % sur une valeur agitée ; en mars
2020, vingt. Le premier réglage traite tous les titres pareil, le second s'ajuste à chacun mais
s'élargit quand le marché s'affole : ce sont deux biais opposés, pas un meilleur et un moins bon.

Les quatre réglages sont facultatifs et se cumulent : à chaque séance, c'est le plus protecteur
des stops configurés qui parle, et les stops priment sur la prise de bénéfice.

Tout cela se joue **en séance**, sur le plus bas et le plus haut du jour même — un stop différé
au lendemain ne veut rien dire — et solde la position entière. Un cours qui ouvre déjà au-delà
du seuil sort à l'ouverture, jamais au seuil. Le niveau opposé à une séance est arrêté à la
clôture de la veille, ATR compris : le stop ne connaît pas la séance qu'il juge.

Une sortie porte son motif dans le relevé de trades : `Signal`, `StopLoss`, `TakeProfit` ou
`TrailingStop`.

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
  brute, mais optimiser sur une seule période reste du surapprentissage : seul `walk-forward`
  juge une stratégie hors échantillon.
- **Fin de fenêtre de test.** Les positions encore ouvertes à la fin d'une fenêtre de test sont
  comptées à leur valeur de clôture, sans frais de sortie.

## Vérification

```bash
dotnet build && dotnet test
```

Les indicateurs sont éprouvés à trois niveaux : un test anti-look-ahead appliqué au catalogue
entier — *aucun indicateur ne dépend d'une barre postérieure à la date qu'il valorise* —, des
micro-séries calculables de tête, et une comparaison différentielle contre
`Skender.Stock.Indicators`, référencé en test uniquement.
