using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Models.StockSales;

namespace Shared.Interface
{
    public interface IOrderService
    {
        Task<IEnumerable<Order>> GetOrdersAsync(string userId);
        Task<Order?> GetOrderByIdAsync(int id);
        Task<Order> CreateOrderAsync(Order order);
    }
}