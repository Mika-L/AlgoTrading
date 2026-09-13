using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Stocks")]
public class Stock
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Symbol { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = [];
}