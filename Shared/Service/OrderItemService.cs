using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Interface;
using Shared.Messaging.Interfaces;
using Shared.Models.StockSales;
using System.Text.Json;

namespace StockService.Services
{
    public class OrderItemService : IOrderItemService
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<OrderItemService> _logger;

        public OrderItemService(AppDbContext context, IRabbitMqClient rabbitMqClient, ILogger<OrderItemService> logger)
        {
            _context = context;
            _rabbitMqClient = rabbitMqClient;
            _logger = logger;
        }

        // 🔑 Buscar itens de um pedido específico do usuário
        public async Task<IEnumerable<OrderItem>> GetItemsByOrderAsync(int orderId, string userId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.CustomerId != userId)
                return new List<OrderItem>();

            return await _context.OrderItems
                .Include(i => i.Product)
                .Where(i => i.OrderId == orderId)
                .AsNoTracking()
                .ToListAsync();
        }

        // 🔑 Adicionar itens a um pedido
        public async Task<IEnumerable<OrderItem>> AddOrderItemsAsync(List<OrderItem> items)
        {
            foreach (var item in items)
            {
                var order = await _context.Orders.FindAsync(item.OrderId);
                if (order == null)
                    throw new Exception($"Pedido {item.OrderId} não existe.");

                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                    throw new Exception($"Produto {item.ProductId} não existe.");

                if (product.StockQuantity < item.Quantity)
                    throw new Exception($"Estoque insuficiente para o produto {product.Name}.");

                if (product.Price != item.UnitPrice)
                    throw new Exception($"Produto {item.ProductId} com valor errado.");

                if (item.UnitPrice <= 0)
                    item.UnitPrice = product.Price;

                product.StockQuantity -= item.Quantity;
                item.TotalPrice = item.UnitPrice * item.Quantity;

                _context.OrderItems.Add(item);

                var orderItemEvent = new
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                };

                await _rabbitMqClient.PublishAsync(
                    JsonSerializer.Serialize(orderItemEvent),
                    "orderitem.created");

                _logger.LogInformation($"Item do pedido {order.Id} criado com sucesso");
            }

            await _context.SaveChangesAsync();
            return items;
        }

        // 🔑 Remover item de pedido
        public async Task DeleteOrderItemAsync(int id)
        {
            var item = await _context.OrderItems.FindAsync(id);
            if (item == null)
                throw new Exception("Item não encontrado");

            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
                product.StockQuantity += item.Quantity;

            var order = await _context.Orders.FindAsync(item.OrderId);
            if (order != null)
                order.TotalAmount -= item.Quantity * item.UnitPrice;

            _context.OrderItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
