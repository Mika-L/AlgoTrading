namespace AlgoTrading.Web.Forms;

/// <summary>
/// Ce qu'un indicateur mesure, comment il se calcule et se lit, et le rôle de chacun de ses
/// paramètres et de chacune de ses lignes.
/// </summary>
public sealed record IndicatorExplanation(
    string Kind,
    string Name,
    string Measures,
    string Computation,
    string Reading,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string> Lines);

/// <summary>
/// Explications des indicateurs du catalogue, telles que l'interface les affiche.
/// <para>Les textes décrivent le calcul tel qu'il est codé dans le domaine, pas la variante
/// d'un manuel : un test vérifie que chaque indicateur, chaque paramètre et chaque ligne y
/// trouve son explication.</para>
/// </summary>
public static class IndicatorGuide
{
    public static IReadOnlyList<IndicatorExplanation> All { get; } =
    [
        new(
            "Atr",
            "Average True Range",
            "La volatilité : l'amplitude moyenne d'une séance, trous d'ouverture compris. Il ne dit rien du sens du marché.",
            "Le true range d'une séance est le plus grand de trois écarts : plus haut − plus bas, plus haut − clôture de la veille, clôture de la veille − plus bas. L'ATR en est la moyenne lissée à la manière de Wilder.",
            "S'exprime en euros, à l'échelle du titre : un seuil fixe n'a de sens que pour un titre donné. Un ATR qui monte signale un marché plus nerveux, sans dire s'il monte ou baisse.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances du lissage. Court, l'ATR réagit vite aux chocs ; long, il décrit la volatilité de fond.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "L'ATR, en euros.",
            }),
        new(
            "Bollinger",
            "Bandes de Bollinger",
            "L'écart du prix à sa moyenne récente, rapporté à sa volatilité.",
            "Bande médiane : moyenne mobile simple des clôtures. Bandes haute et basse : la médiane plus ou moins un multiple de l'écart type des clôtures sur la même fenêtre.",
            "Les bandes s'écartent quand la volatilité monte et se resserrent quand elle baisse. Une clôture hors des bandes est un écart inhabituel : on peut y voir un excès appelé à se résorber (retour à la moyenne) ou le départ d'un mouvement (suivi de tendance).",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances de la moyenne et de l'écart type.",
                ["multiplier"] = "Nombre d'écarts types entre la médiane et chaque bande. À 2, une clôture sort des bandes environ une séance sur vingt ; plus haut, les sorties sont plus rares et plus marquées.",
            },
            new Dictionary<string, string>
            {
                ["middle"] = "Moyenne mobile simple des clôtures.",
                ["upper"] = "Médiane + multiplicateur × écart type.",
                ["lower"] = "Médiane − multiplicateur × écart type.",
            }),
        new(
            "Cci",
            "Commodity Channel Index",
            "L'écart du prix à sa moyenne, en unités de dispersion habituelle.",
            "Prix typique = (plus haut + plus bas + clôture) ÷ 3. CCI = (prix typique − sa moyenne simple) ÷ (0,015 × écart absolu moyen). La constante 0,015 cale environ trois quarts des valeurs entre −100 et +100.",
            "Oscille autour de 0, sans bornes. Au-delà de +100, le prix est nettement au-dessus de sa moyenne (suracheté, ou tendance haussière vigoureuse) ; en deçà de −100, nettement au-dessous.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances de la moyenne et de l'écart absolu moyen.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le CCI, sans unité.",
            }),
        new(
            "Ema",
            "Moyennes mobiles exponentielles",
            "La tendance, par la comparaison d'une moyenne rapide et d'une moyenne lente des clôtures.",
            "Deux moyennes mobiles exponentielles des clôtures : chaque séance pèse plus que la précédente, ce qui les rend plus réactives qu'une moyenne simple de même période.",
            "Quand la moyenne rapide passe au-dessus de la lente, la hausse récente l'emporte sur la tendance de fond : signal haussier. Le croisement inverse est baissier. Les croisements arrivent toujours en retard sur le retournement, et se multiplient sans rien dire quand le marché fait du surplace.",
            new Dictionary<string, string>
            {
                ["fast"] = "Période de la moyenne rapide, en séances. Doit rester inférieure à la période lente.",
                ["slow"] = "Période de la moyenne lente, en séances.",
            },
            new Dictionary<string, string>
            {
                ["fast"] = "Moyenne exponentielle rapide.",
                ["slow"] = "Moyenne exponentielle lente.",
            }),
        new(
            "Ichimoku",
            "Ichimoku Kinko Hyo",
            "La tendance et ses zones de soutien, à partir des milieux de canaux de plus haut et de plus bas.",
            "Tenkan et Kijun : milieu entre le plus haut et le plus bas de leur fenêtre. Senkou A : moyenne de Tenkan et Kijun ; Senkou B : milieu du canal sur la fenêtre longue ; toutes deux reportées de « décalage » séances vers l'avant, si bien que le nuage du jour a été tracé il y a « décalage » séances. La Chikou Span n'est pas calculée : elle lirait l'avenir.",
            "Le nuage est l'espace entre Senkou A et Senkou B. Une clôture au-dessus du nuage, avec Senkou A au-dessus de Senkou B, décrit une tendance haussière ; l'inverse une tendance baissière ; dans le nuage, le marché hésite. Le croisement Tenkan / Kijun sert de signal plus rapide.",
            new Dictionary<string, string>
            {
                ["tenkan"] = "Fenêtre de la ligne de conversion (Tenkan), en séances.",
                ["kijun"] = "Fenêtre de la ligne de base (Kijun), en séances.",
                ["senkouB"] = "Fenêtre du bord lent du nuage (Senkou B), en séances.",
                ["displacement"] = "Nombre de séances dont le nuage est reporté vers l'avant.",
            },
            new Dictionary<string, string>
            {
                ["tenkan"] = "Ligne de conversion, rapide.",
                ["kijun"] = "Ligne de base, lente.",
                ["senkouA"] = "Premier bord du nuage, (Tenkan + Kijun) ÷ 2 décalé.",
                ["senkouB"] = "Second bord du nuage, milieu du canal long décalé.",
            }),
        new(
            "Keltner",
            "Canal de Keltner",
            "L'écart du prix à sa moyenne, rapporté à l'amplitude moyenne des séances.",
            "Ligne médiane : moyenne mobile exponentielle des clôtures. Bandes : la médiane plus ou moins un multiple de l'ATR calculé sur la même période.",
            "Proche des bandes de Bollinger, mais plus régulier : l'ATR varie moins brutalement qu'un écart type. Une clôture hors du canal se lit en retour à la moyenne ou en suivi de tendance.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances de la moyenne exponentielle et de l'ATR.",
                ["multiplier"] = "Nombre d'ATR entre la médiane et chaque bande. Plus il est grand, plus les sorties de canal sont rares.",
            },
            new Dictionary<string, string>
            {
                ["middle"] = "Moyenne mobile exponentielle des clôtures.",
                ["upper"] = "Médiane + multiplicateur × ATR.",
                ["lower"] = "Médiane − multiplicateur × ATR.",
            }),
        new(
            "Macd",
            "Moving Average Convergence Divergence",
            "La force et le sens de la tendance, par l'écart entre deux moyennes exponentielles.",
            "MACD = moyenne exponentielle rapide − moyenne exponentielle lente des clôtures. Signal = moyenne exponentielle du MACD. Histogramme = MACD − signal.",
            "Un MACD positif veut dire que la moyenne rapide est au-dessus de la lente. Le passage du MACD au-dessus de sa ligne de signal (histogramme qui devient positif) indique une accélération haussière ; l'inverse un essoufflement. S'exprime en euros, à l'échelle du titre.",
            new Dictionary<string, string>
            {
                ["fast"] = "Période de la moyenne rapide, en séances. Doit rester inférieure à la période lente.",
                ["slow"] = "Période de la moyenne lente, en séances.",
                ["signal"] = "Période de la moyenne exponentielle du MACD qui forme la ligne de signal.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le MACD : rapide − lente.",
                ["signal"] = "Moyenne exponentielle du MACD.",
                ["histogram"] = "MACD − signal.",
            }),
        new(
            "Momentum",
            "Momentum",
            "La vitesse du prix : de combien il a varié sur la période.",
            "Clôture du jour − clôture d'il y a « période » séances.",
            "Positif, le prix est plus haut qu'il y a « période » séances ; négatif, plus bas. Le signe sert de filtre de tendance, avec un pivot à 0. S'exprime en euros, à l'échelle du titre.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances entre les deux clôtures comparées.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "La variation de clôture, en euros.",
            }),
        new(
            "Rsi",
            "Relative Strength Index",
            "L'équilibre entre hausses et baisses récentes, sur une échelle de 0 à 100.",
            "Moyennes des hausses et des baisses de clôture, lissées à la manière de Wilder. RSI = 100 − 100 ÷ (1 + hausse moyenne ÷ baisse moyenne). Sans aucune baisse sur la fenêtre, il vaut 100.",
            "Au-dessus de 70, les hausses ont dominé au point que le titre passe pour suracheté ; sous 30, pour survendu. Ces seuils se règlent dans la règle. En forte tendance, le RSI peut rester longtemps en zone extrême.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances du lissage. Court, le RSI touche plus souvent les extrêmes.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le RSI, de 0 à 100.",
            }),
        new(
            "Sar",
            "Parabolic SAR",
            "Un point de retournement qui suit la tendance et s'en rapproche de plus en plus vite (Stop And Reverse).",
            "En hausse, le SAR part sous les cours et monte à chaque séance d'une fraction de l'écart au plus haut atteint ; cette fraction démarre au pas et grandit d'un pas à chaque nouveau plus haut, jusqu'au plafond. Il ne dépasse jamais les plus bas des deux séances précédentes. Quand le prix le franchit, la tendance s'inverse et le calcul repart de l'autre côté. Symétrique en baisse.",
            "Clôture au-dessus du SAR : tendance haussière ; au-dessous : baissière. Le SAR sert aussi de niveau de stop suiveur.",
            new Dictionary<string, string>
            {
                ["step"] = "Facteur d'accélération initial, et incrément à chaque nouvel extrême. Plus grand, le SAR serre les cours plus vite et se retourne plus souvent.",
                ["maxStep"] = "Plafond du facteur d'accélération. Ne peut pas être inférieur au pas.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le niveau du SAR, en euros.",
            }),
        new(
            "Stochastic",
            "Oscillateur stochastique",
            "La position de la clôture dans l'amplitude récente, de 0 (au plus bas) à 100 (au plus haut).",
            "%K = 100 × (clôture − plus bas) ÷ (plus haut − plus bas), plus haut et plus bas pris sur la période. %D = moyenne simple de %K sur la période de signal.",
            "Au-dessus de 80, le titre clôture près de ses plus hauts récents (suracheté) ; sous 20, près de ses plus bas (survendu). Le croisement de %K et %D sert de signal.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances sur lesquelles se prennent le plus haut et le plus bas.",
                ["signal"] = "Nombre de séances de la moyenne de %K qui forme %D.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "%K, de 0 à 100.",
                ["signal"] = "%D, moyenne de %K.",
            }),
        new(
            "Vwap",
            "VWAP glissant",
            "Le prix moyen auquel les titres ont réellement changé de mains sur la période.",
            "Moyenne des prix typiques (plus haut + plus bas + clôture) ÷ 3 pondérée par les volumes, sur une fenêtre glissante. Une fenêtre sans aucun échange retombe sur la moyenne simple des prix typiques.",
            "Une clôture au-dessus du VWAP signifie que le prix dépasse ce qu'ont payé en moyenne les acheteurs récents : pression acheteuse ; au-dessous, pression vendeuse.",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances de la fenêtre glissante.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le VWAP, en euros.",
            }),
        new(
            "WilliamsR",
            "Williams %R",
            "La distance de la clôture au plus haut récent, de −100 (au plus bas) à 0 (au plus haut).",
            "%R = −100 × (plus haut − clôture) ÷ (plus haut − plus bas), sur la période. C'est le %K du stochastique décalé de 100.",
            "Au-dessus de −20, le titre clôture près de ses plus hauts (suracheté) ; sous −80, près de ses plus bas (survendu).",
            new Dictionary<string, string>
            {
                ["period"] = "Nombre de séances sur lesquelles se prennent le plus haut et le plus bas.",
            },
            new Dictionary<string, string>
            {
                ["value"] = "Le %R, de −100 à 0.",
            }),
    ];

    private static readonly Dictionary<string, IndicatorExplanation> ByKind =
        All.ToDictionary(static e => e.Kind, StringComparer.Ordinal);

    public static IndicatorExplanation? Find(string kind) => ByKind.GetValueOrDefault(kind);
}
