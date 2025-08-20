// Models/OrderItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models
{
    public class OrderItem
    {
          public int Id { get; set; }
          public int OrderId { get; set; }
          public Order? Order { get; set; }   // <<< removido required
          public int ProductId { get; set; }
          public Product? Product { get; set; }  // <<< removido required
          public int Quantity { get; set; }
          public decimal UnitPrice { get; set; }
          public decimal TotalPrice { get; set; }
    }

}
