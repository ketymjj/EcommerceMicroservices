using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Interface;
using Shared.Messaging.Interfaces;
using Shared.Models.StockSales;
using System.Text.Json;

namespace StockService.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext context, IRabbitMqClient rabbitMqClient, ILogger<OrderService> logger)
        {
            _context = context;
            _rabbitMqClient = rabbitMqClient;
            _logger = logger;
        }

        // 🔑 Buscar pedidos de um usuário específico
        public async Task<IEnumerable<Order>> GetOrdersAsync(string userId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        // 🔑 Buscar pedido por Id
        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        // 🔑 Criar novo pedido e publicar evento no RabbitMQ
        public async Task<Order> CreateOrderAsync(Order order)
        {
            var orderToSave = new Order
            {
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                OrderDate = DateTime.UtcNow
            };

            _context.Orders.Add(orderToSave);
            await _context.SaveChangesAsync();

            // Publicar evento
            var orderCreatedEvent = new
            {
                OrderId = orderToSave.Id,
                CustomerId = orderToSave.CustomerId,
                TotalAmount = orderToSave.TotalAmount
            };

            await _rabbitMqClient.PublishAsync(
                JsonSerializer.Serialize(orderCreatedEvent),
                "order.created");

            _logger.LogInformation($"Pedido {orderToSave.Id} criado com sucesso");

            return orderToSave;
        }
    }
}
