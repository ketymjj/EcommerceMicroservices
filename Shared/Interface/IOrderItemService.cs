using Shared.Models.StockSales;

namespace Shared.Interface
{
    public interface IOrderItemService
    {
        Task<IEnumerable<OrderItem>> GetItemsByOrderAsync(int orderId, string userId);
        Task<IEnumerable<OrderItem>> AddOrderItemsAsync(List<OrderItem> items);
        Task DeleteOrderItemAsync(int id);
    }
}
