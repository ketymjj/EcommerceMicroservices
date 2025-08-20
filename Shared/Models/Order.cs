
using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

         [Required]
        public required string CustomerId { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
