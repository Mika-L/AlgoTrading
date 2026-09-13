using System;
using ConsoleApp1.Models;
using ConsoleApp1.Repository;

namespace ConsoleApp1;

public class Porfolio
{
    private readonly decimal initCash;
    private readonly string name;
    public decimal Cash { get; private set; }
    public int Stocks { get; private set; }
    public decimal Profit { get; private set; }
    public decimal Performance { get; private set; }

    public Porfolio(string name, decimal value, int stocks)
    {
        this.name = name;
        Cash = value;
        initCash = value;
        Stocks = stocks;
    }

    public void Reset()
    {
        Cash = initCash;
        Stocks = 0;
    }

    public void CalculatePerf(decimal price)
    {
        Profit = (int)(GetValue(price) - initCash);
        Performance = (GetValue(price) - initCash) / initCash * 100;
    }

    public int Buy(int stocks, decimal price)
    {
        int stocksToBuy = Math.Min(stocks, MaxPossibleBuyStocks(price));

        Cash -= stocksToBuy * price;
        Stocks += stocksToBuy;

        return stocksToBuy;
    }

    public int Buy(decimal price, decimal totalAmount)
    {
        int stocksToBuy = Math.Min(MaxPossibleBuyStocks(price), (int)(totalAmount / price));

        Cash -= stocksToBuy * price;
        Stocks += stocksToBuy;

        return stocksToBuy;
    }

    public int MaxPossibleBuyStocks(decimal price)
    {
        return (int)(Cash / price);
    }

    public void Sell(int stocks, decimal price)
    {
        var stocksToSell = Math.Min(stocks, Stocks);
        Cash += stocksToSell * price;
        Stocks -= stocksToSell;
    }

    public decimal GetValue(decimal price)
    {
        return Cash + Stocks * price;
    }

    public override string ToString()
    {
        return $"{name}|${Profit}|{Performance:0.00}%";
    }
}
