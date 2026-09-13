using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


[Table("Orders")]
public class Order
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public int StockId { get; set; }

    [ForeignKey(nameof(StockId))]
    public virtual Stock? Stock { get; set; }

    [Required]
    public decimal Price { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public OrderType Type { get; set; }
}