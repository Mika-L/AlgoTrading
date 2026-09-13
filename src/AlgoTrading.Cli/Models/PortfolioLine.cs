public class PortoflioLine
{
    public required Stock Stock { get; set; }
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }

    public void AddShares(int quantity, decimal price)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        if (price <= 0)
            throw new ArgumentException("Price must be greater than 0", nameof(price));

        var totalCost = AveragePrice * Quantity + price * quantity;
        Quantity += quantity;
        AveragePrice = totalCost / Quantity;
    }

    public void SellShares(int quantity, decimal price)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        if (price <= 0)
            throw new ArgumentException("Price must be greater than 0", nameof(price));
        if (quantity > Quantity)
            throw new InvalidOperationException("Cannot sell more shares than owned");


        var totalCost = AveragePrice * Quantity - price * quantity;
        Quantity -= quantity;

        AveragePrice = totalCost / Quantity;
    }
}