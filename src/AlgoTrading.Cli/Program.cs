using ConsoleApp1.Models;
using ConsoleApp1.Indicators;
using ConsoleApp1;
using ConsoleApp1.Strategy;
using System.Diagnostics;
using ConsoleApp1.Repository;

class Program
{
    static void Main()
    {
        // ResetDatabase();
        // InitDatabase();

        //SimulateTradingMonoAsset();
        //SimulateTradingMultiStocks();

        GenerateTradeCharts();
        
        Console.WriteLine();
    }

    static void ResetDatabase()
    {
        using (var db = new StockDbContext())
        {
            db.Database.EnsureDeleted();
        }
    }

    static void InitDatabase()
    {
        using (var db = new StockDbContext())
        {
            db.Database.EnsureCreated();

            if (!db.StockPriceHistories.Any())
            {
                // var stockGle = new Stock { Name = "Société Générale", Symbol = "GLE.PA" };
                // db.Stocks.Add(stockGle);
                // var stockCarr = new Stock { Name = "Carrefour", Symbol = "CA.PA" };
                // db.Stocks.Add(stockCarr);
                // var stockDas = new Stock { Name = "Dassault Systèmes", Symbol = "DSY.PA" };
                // db.Stocks.Add(stockDas);
                // var stockStm = new Stock { Name = "STMicroelectronics", Symbol = "STM" }; // STM.PA is not a valid ticker for yahoo finance
                // db.Stocks.Add(stockStm);
                // var stockAir = new Stock { Name = "Air Liquide", Symbol = "AI.PA" };
                // db.Stocks.Add(stockAir);
                // var stockAcc = new Stock { Name = "Accor", Symbol = "AC.PA" };
                // db.Stocks.Add(stockAcc);
                // db.SaveChanges();

                //var dataProvider = new InvestingDataProvider();
                var dataProvider = new YahooFinanceDownloader();

                YahooFinanceDownloader.Cac40Tickers.ForEach(ticker =>
                {
                    db.Stocks.Add(new Stock { Name = ticker, Symbol = ticker });
                });
                db.SaveChanges();

                foreach (var stock in db.Stocks)
                {
                    Console.WriteLine($"Downloading data for {stock.Name} ({stock.Symbol})...");
                    var stockPrices = dataProvider.GetHistoricalDataAsync(stock.Symbol, new DateTime(2017, 1, 1), DateTime.Now).Result;
                    stockPrices.ForEach(p => p.StockId = stock.Id);
                    SaveToDatabase(db, stockPrices);
                    Console.WriteLine($"Downloaded {stockPrices.Count} records for {stock.Name}.");
                }
                Console.WriteLine("Database initialized with stock data.");
            }
        }
    }

    static void SaveToDatabase(StockDbContext context, List<StockPriceHistory> stockPrices)
    {
        context.StockPriceHistories.AddRange(stockPrices);
        context.SaveChanges();
    }

    static List<Stock> GetStocks()
    {
        using var db = new StockDbContext();
        return db.Stocks
            .Where(p => p.Id != 6)
            .ToList();
    }

    static List<StockPriceHistory> GetPricesForStockId(int take, int stockId)
    {
        using var db = new StockDbContext();

        var results = db.StockPriceHistories
            .OrderByDescending(p => p.Date)
            .Where(p => p.StockId == stockId)
            .Take(take)
            .ToList();

        return results.OrderBy(p => p.Date).ToList();
    }

    static List<StockPriceHistory> GetPricesForStockId(int stockId, DateTime startDate, DateTime endDate)
    {
        using var db = new StockDbContext();

        var results = db.StockPriceHistories
            .Where(p => p.StockId == stockId)
            .Where(p => p.Date >= startDate && p.Date <= endDate)
            .OrderBy(p => p.Date)
            .ToList();

        return results.ToList();
    }

    static Dictionary<int, List<StockPriceHistory>> GetPricesForAllStocks()
    {
        using var db = new StockDbContext();
        return db.StockPriceHistories
            .Where(p => p.StockId != 6)
            .OrderBy(p => p.Date)
            .GroupBy(p => p.StockId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    static void SimulateTradingMonoAsset()
    {
        decimal capitalInit = 10000;

        var portfolioBH = new Porfolio("Buy&Hold", capitalInit, 0);
        var portfolioS = new Porfolio("Sizing", capitalInit, 0);
        var portfolioB = new Porfolio("Binary", capitalInit, 0);

        decimal capital = capitalInit;
        decimal positionSize = 0.1m; // 10% of capital
        decimal stopLoss = 0.02m; // 2% of capital
        decimal takeProfit = 0.04m; // 4% of capital

        var prices = GetPricesForStockId(10 * 365, 3);
        IIndicator indicator;

        // var emaIndicator = new MME(prices, 9, 20);
        // Buy&Hold|$-1226|-12,27%
        // Binary|$3704|37,04%
        // Sizing|$2619|26,19%
        //var indicator = new MME(prices, 50, 200);
        // Simulation results:
        // Name|Profit|Performance
        // Buy&Hold|$3043|30,43%
        // Binary|$-5722|-57,22%
        // Sizing|$1442|14,43%
        //var indicator = new RSI(prices);
        // Simulation results:
        // Name|Profit|Performance
        // Buy&Hold|$3043|30,43%
        // Binary|$-2684|-26,85%
        // Sizing|$-196|-1,97%
        //indicator = new BollingerBand(prices);
        // Simulation results:
        // Name|Profit|Performance
        // Buy&Hold|$3043|30,43%
        // Binary|$7872|78,73%
        // Sizing|$9746|97,47%

        indicator = new IndicatorAggregator([
            new MME(prices, 50, 200),
            new BollingerBand(prices),
            new RSI(prices),
            new VWAP(prices)
        ], IndicatorAggregator.AggregationStrategy.Majority, 0.5m);
        // Simulation results:
        // Name|Profit|Performance
        // Buy&Hold|$3043|30,43%
        // Binary|$2944|29,45%
        // Sizing|$6129|61,29%

        ScenarioBinary(portfolioB, prices, indicator);
        ScenarioSized(portfolioS, prices, indicator, positionSize);
        ScenarioBuyHold(portfolioBH, prices);

        portfolioBH.CalculatePerf(prices.Last().Close);
        portfolioS.CalculatePerf(prices.Last().Close);
        portfolioB.CalculatePerf(prices.Last().Close);

        Console.WriteLine("Simulation results:");
        Console.WriteLine("Name|Profit|Performance");
        Console.WriteLine(portfolioBH.ToString());
        Console.WriteLine(portfolioB.ToString());
        Console.WriteLine(portfolioS.ToString());

    }

    static void SimulateTradingMultiStocks()
    {
        decimal capitalInit = 10000;
        decimal positionSize = 0.1m; // 10% of capital

        var stocks = GetStocks();
        var priceHisto = GetPricesForAllStocks();

        // Console.WriteLine($"Found {priceHisto.Count} stocks with historical data.");
        // var firstPrice = priceHisto.First().Value.First();
        // Console.WriteLine(firstPrice.ToString());

        // var dataProvider = new InvestingDataProvider();
        // var csvPrice = dataProvider.GetHistoricalDataAsync("GLE.PA", new DateTime(2017, 1, 1), DateTime.Now).Result;
        // var firstCsvPrice = csvPrice.First();
        // Console.WriteLine(firstCsvPrice.ToString());

        var priceDic = priceHisto.ToDictionary(kvp =>
            stocks.First(s => s.Id == kvp.Key),
            kvp => kvp.Value.OrderBy(p => p.Date).ToList()
        );

        decimal capital = capitalInit;

        // var indicators = new List<IIndicator>();
        var indicators = new Dictionary<Stock, IIndicator>();

        var startDate = new DateTime(2017, 1, 1);
        var endDate = new DateTime(2025, 1, 1);

        var indicatorTypes = new List<Func<List<StockPriceHistory>, IIndicator>>()
        {
            // prices => new MME(prices, 50, 200),
            // prices => new BollingerBand(prices),
            // prices => new RSI(prices),
            // prices => new VWAP(prices),
            price => new CCI(price, 20),
            // prices => new Momentum(prices, 10),
            // prices => new MACD(prices, 12, 26, 9),
            // prices => new StochasticOscillator(prices, 14),
            // prices => new WilliamsR(prices, 14),
            prices => new ATR(prices, 14),
            // prices => new KeltnerChannel(prices, 20, 1.5m),
            prices => new ParabolicSAR(prices, 0.02m, 0.2m),
            prices => new Ichimoku(prices),

            // prices => new ADX(prices, 14),
            // prices => new VolumeWeightedAveragePrice(prices),
            // prices => new PriceChannel(prices, 20),
            // prices => new DonchianChannel(prices, 20),
            // prices => new ChaikinOscillator(prices, 10, 20),
            // prices => new ElderRay(prices, 13),
            // prices => new FisherTransform(prices, 10),
            // prices => new MassIndex(prices, 9),
            // prices => new RateOfChange(prices, 10),
            // prices => new UltimateOscillator(prices, 7, 14, 28),
            // prices => new VortexIndicator(prices, 14),
            // prices => new TrueRange(prices),
            // prices => new PriceOscillator(prices, 12, 26),
            // prices => new CoppockCurve(prices, 14, 11, 10),
            // prices => new GopalakrishnanRangeIndex(prices, 14),
            // prices => new ChandeKrollStop(prices, 20, 3.0m),
            // prices => new ConnorsRSI(prices, 3, 2, 100),
            // prices => new DetrendedPriceOscillator(prices, 20),
            // prices => new KlingerOscillator(prices, 34, 55, 13),
            // prices => new McGinleyDynamic(prices, 14),
            // prices => new PriceVolumeTrend(prices),
            // prices => new RelativeVigorIndex(prices, 14),
            // prices => new SchaffTrendCycle(prices, 10, 23, 50),
            // prices => new TripleExponentialMovingAverage(prices, 15),
            // prices => new WilliamsAlligator(prices, 13, 8, 5),
            // prices => new ZLEMA(prices, 20),
            // prices => new ChoppinessIndex(prices, 14),
            // prices => new KST(prices, 10, 15, 20, 30, 10, 15, 20, 30),
            // prices => new MassIndex(prices, 9),
            // prices => new PriceMomentumOscillator(prices, 10),
            // prices => new RelativeStrengthIndex(prices, 14),
            // prices => new SuperTrend(prices, 10, 3.0m),
            // prices => new Trix(prices, 15),
            // prices => new VortexIndicator(prices, 14),
            // prices => new WilliamsFractal(prices, 5),
            // prices => new ZScore(prices, 20),
            // prices => new AdaptiveMovingAverage(prices, 10),
            // prices => new ChandeMomentumOscillator(prices, 20),
            // prices => new DetrendedPriceOscillator(prices, 20),
            // prices => new FisherTransform(prices, 10),
            // prices => new GopalakrishnanRangeIndex(prices, 14),
            // prices => new HilbertTransform(prices, 9),
            // prices => new KeltnerChannel(prices, 20, 1.5m),
            // prices => new LinearRegression(prices, 20),
            // prices => new MovingAverageConvergenceDivergence(prices, 12, 26, 9),
            // prices => new PriceOscillator(prices, 12, 26),
            // prices => new RelativeStrengthIndex(prices, 14),
            // prices => new SchaffTrendCycle(prices, 10, 23, 50),
            // prices => new TrueStrengthIndex(prices, 14, 25),
            // prices => new VolumeOscillator(prices, 14, 28),
            // prices => new WilliamsR(prices, 14),
            // prices => new ZLEMA(prices, 20),
            // prices => new AdaptiveMovingAverage(prices, 10),
            // prices => new ChandeKrollStop(prices, 20, 3.0m),
            // prices => new ConnorsRSI(prices, 3, 2, 100),
            // prices => new DetrendedPriceOscillator(prices, 20),
            // prices => new KlingerOscillator(prices, 34, 55, 13),
            // prices => new McGinleyDynamic(prices, 14),
            // prices => new PriceVolumeTrend(prices),
            // prices => new RelativeVigorIndex(prices, 14),
            // prices => new SchaffTrendCycle(prices, 10, 23, 50),
            // prices => new TripleExponentialMovingAverage(prices, 15),
            // prices => new WilliamsAlligator(prices, 13, 8, 5),
        };


        var stockIndicators = new Dictionary<Stock, List<IIndicator>>();
        foreach (var stock in stocks)
        {
            var prices = GetPricesForStockId(stock.Id, startDate, endDate);
            stockIndicators[stock] = indicatorTypes.Select(f => f(prices)).ToList();
        }

        PorfolioMulti bestPortfolio = null;
        string bestCombination = string.Empty;
        List<IIndicator> indicatorInstances = new List<IIndicator>();

        using var dbContext = new StockDbContext();
        var orderRepository = new OrderRepository(dbContext);

        for (int k = 4; k <= indicatorTypes.Count; k++)
        {
            foreach (var combination in GetCombinations(indicatorTypes, k))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var portfolioS = new PorfolioMulti("Sizing", capitalInit, priceDic, orderRepository);
                foreach (var stock in stocks)
                {
                    // portfolioS = new PorfolioMulti("Sizing", capitalInit, priceDic);

                    var prices = GetPricesForStockId(stock.Id, startDate, endDate);
                    var tmpIndicatorInstances = combination.Select(f => f(prices)).ToList();
                    indicatorInstances = stockIndicators[stock].Where(i => tmpIndicatorInstances.Select(t => t.GetType()).Contains(i.GetType())).ToList();

                    var aggregator = new IndicatorAggregator(
                        indicatorInstances,
                        IndicatorAggregator.AggregationStrategy.Majority,
                        0.5m
                    );
                    indicators.Add(stock, aggregator);

                    // ScenarioSizedMulti(portfolioS, indicators, positionSize, startDate, endDate);
                    // portfolioS.CalculatePerf();
                    // Console.WriteLine($"{stock.Name} | {portfolioS.Profit + capitalInit:F2} | {portfolioS.Performance:0.00}%");
                }

                var combinationName = string.Join(", ", indicatorInstances.Select(i => i.GetType().Name));

                ScenarioSizedMulti(portfolioS, indicators, positionSize, startDate, endDate);
                portfolioS.CalculatePerf();

                if (bestPortfolio == null || portfolioS.Performance > bestPortfolio.Performance)
                {
                    bestPortfolio = portfolioS;
                    bestCombination = combinationName;
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"{combinationName}|{portfolioS.Profit + capitalInit:F2}|{portfolioS.Performance:0.00}%");
                Console.WriteLine($"Time taken: {stopwatch.ElapsedMilliseconds} ms");

                indicators.Clear(); // Nettoie pour la prochaine combinaison
            }
        }

        Console.WriteLine($"Best portfolio with performance {bestPortfolio.Performance:0.00}% and profit {bestPortfolio.Profit:C}");
        Console.WriteLine($"Best combination: {bestCombination}");
    }

    static IEnumerable<IEnumerable<T>> GetCombinations<T>(IEnumerable<T> list, int length)
    {
        if (length == 0) yield return Enumerable.Empty<T>();
        else
        {
            int i = 0;
            foreach (var item in list)
            {
                var remaining = list.Skip(i + 1);
                foreach (var combination in GetCombinations(remaining, length - 1))
                    yield return new[] { item }.Concat(combination);
                i++;
            }
        }
    }
    private static void ScenarioBinary(Porfolio portfolio, List<StockPriceHistory> prices, IIndicator emaIndicator)
    {
        foreach (var price in prices)
        {
            // v1
            // buy all / sell all on bullish/bearish signal
            if (emaIndicator.IsBearish(price.Date))
            {
                // Sell all
                portfolio.Sell(portfolio.Stocks, price.Close);
                // Console.WriteLine($"{price.Date:yyyy-MM-dd} - Sell {stocks} @ ${price.Price} - ${portfolio.GetValue(price.Price)}");

                // Sell
                // decimal positionValue = capital * positionSize;
                // decimal stopLossValue = capital * stopLoss;
                // decimal takeProfitValue = capital * takeProfit;

                // if (price.Price > stopLossValue)
                // {
                //     capital -= positionValue;
                //     Console.WriteLine($"{price.Date:yyyy-MM-dd} - Sell {positionValue} - {capital}");
                // }
            }
            else if (emaIndicator.IsBullish(price.Date))
            {
                // Buy all
                int stocksBought = portfolio.Buy(price.Close, portfolio.Cash);
                // Console.WriteLine($"{price.Date:yyyy-MM-dd} - Buy {stocksBought} @ ${price.Price} - ${portfolio.GetValue(price.Price)}");

                // Buy
                // decimal positionValue = capital * positionSize;
                // decimal stopLossValue = capital * stopLoss;
                // decimal takeProfitValue = capital * takeProfit;

                // if (price.Price < stopLossValue)
                // {
                //     capital += positionValue;
                //     Console.WriteLine($"{price.Date:yyyy-MM-dd} - Buy {positionValue} - {capital}");
                // }
            }
        }
    }

    private static void ScenarioSized(Porfolio portfolio, List<StockPriceHistory> prices, IIndicator indicator, decimal positionSize)
    {
        foreach (var price in prices)
        {

            // 10% buy / sell on bullish/bearish signal
            if (indicator.IsBearish(price.Date) && portfolio.Stocks > 0)
            {
                int stocks = (int)(portfolio.Stocks * positionSize);
                portfolio.Sell(stocks, price.Close);
                // Console.WriteLine($"{price.Date:yyyy-MM-dd} - Sell {stocks} @ ${price.Price} - ${portfolio.GetValue(price.Price)}");
            }
            else if (indicator.IsBullish(price.Date))
            {
                int stocks = portfolio.Buy(price.Close, portfolio.Cash * positionSize);

                // Console.WriteLine($"{price.Date:yyyy-MM-dd} - Buy {stocks} @ ${price.Price} - ${portfolio.GetValue(price.Price)}");
            }
        }
    }

    private static void ScenarioSizedMulti(PorfolioMulti portfolio, Dictionary<Stock, IIndicator> indicators, decimal positionSize, DateTime startDate, DateTime endDate)
    {
        var currentDate = startDate;
        var history = new List<(DateTime Date, decimal Value)>();
        while (currentDate < endDate)
        {
            foreach (var indicator in indicators)
            {
                if (indicator.Value.IsBearish(currentDate))
                {
                    portfolio.Sell(indicator.Key, currentDate, indicator.Value.GetPrice(currentDate));
                    //Console.ForegroundColor = ConsoleColor.Red;
                    // Console.WriteLine($"{currentDate:yyyy-MM-dd} - Sell {indicator.Key.Name} @ ${indicator.Value.GetPrice(currentDate)} - ${portfolio.GetPositionValue(indicator.Key, indicator.Value.GetPrice(currentDate))}");
                }
                else if (indicator.Value.IsBullish(currentDate))
                {
                    portfolio.Buy(indicator.Key, currentDate, indicator.Value.GetPrice(currentDate), portfolio.Cash * positionSize);
                    //Console.ForegroundColor = ConsoleColor.Green;
                    // Console.WriteLine($"{currentDate:yyyy-MM-dd} - Buy {indicator.Key.Name} @ ${indicator.Value.GetPrice(currentDate)} - ${portfolio.GetPositionValue(indicator.Key, indicator.Value.GetPrice(currentDate))}");
                }
            }


            var netWorth = portfolio.GetNetWorth(currentDate);
            history.Add((currentDate, netWorth));

            currentDate = currentDate.AddDays(1);
        }

        File.WriteAllLines("portfolio_multi.csv", history.Select(h => $"{h.Date:yyyy-MM-dd},{h.Value:F2}"));
    }

    // buy and hold
    private static void ScenarioBuyHold(Porfolio portfolio, List<StockPriceHistory> prices)
    {
        var priceFirstDay = prices.First().Close;
        var stocks = (int)(portfolio.Cash / priceFirstDay);
        portfolio.Buy(stocks, priceFirstDay);
        // Console.WriteLine($"{prices.First().Date:yyyy-MM-dd} - Buy {stocks} - {portfolio.GetValue(prices.Last().Price)}");
    }

    public static void GenerateTradeCharts()
    {
        using var context = new TradingContext();

        // Analyser les trades
        var tradeService = new TradeAnalysisService(context);
        var trades = tradeService.CalculateTrades();

        if (!trades.Any())
        {
            Console.WriteLine("Aucun trade complet trouvé (paires achat/vente).");
            return;
        }

        // Générer les visualisations
        var visualizationService = new TradeVisualizationService();

        visualizationService.GenerateTradeChart(trades);
        visualizationService.GenerateCumulativeChart(trades);
        visualizationService.PrintTradesSummary(trades);

        Console.WriteLine("\nGraphiques générés avec succès !");
    }
}