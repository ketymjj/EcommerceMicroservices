using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class OrderCreatedEvent
    {
        public int OrderId { get; set; }
        public string? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemEvent> Items { get; set; } = new();
    
        public record OrderItemEvent(int ProductId, int Quantity);
    }
}