using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging.Interfaces;
using System.Text.Json;
using Shared.Security;
using Shared.Security.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Shared.Models.StockSales; // Namespace onde está JwtTokenValidator

namespace StockService.SalesService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderItemsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<OrderItemsController> _logger;
        private readonly IJwtTokenService _jwtValidator;

        public OrderItemsController(
            AppDbContext context,
            IRabbitMqClient rabbitMqClient,
            ILogger<OrderItemsController> logger,
            IJwtTokenService jwtValidator)
        {
            _context = context;
            _rabbitMqClient = rabbitMqClient;
            _logger = logger;
            _jwtValidator = jwtValidator;
        }

        // GET: api/orderitems/{orderId}
        [HttpGet("{orderId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderItem>>> GetItemsByOrder(int orderId)
        {
            var principal = ValidateRequestToken();
            if (principal == null)
                return Unauthorized("Token inválido ou não informado");
        
            // Obtém o Id do usuário logado do token (claim "sub" ou "nameidentifier")
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Usuário não identificado no token");
        
            try
            {
                // Verifica se o pedido realmente pertence ao usuário logado
                var order = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);
        
                if (order == null)
                    return NotFound("Pedido não encontrado para este usuário.");
        
                var items = await _context.OrderItems
                    .Include(i => i.Product)
                    .Where(i => i.OrderId == orderId)
                    .AsNoTracking()
                    .ToListAsync();
        
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar itens do pedido {orderId}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }


        // POST: api/orderitems
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderItem>>> AddOrderItems([FromBody] List<OrderItem> items)
        {

            var principal = ValidateRequestToken();

            if (principal == null)
                return Unauthorized("Token inválido ou não informado");

            if (items == null || !items.Any())
                return BadRequest("Nenhum item enviado.");

            try
            {
                foreach (var item in items)
                {
                    var order = await _context.Orders.FindAsync(item.OrderId);
                    if (order == null)
                        return BadRequest($"Pedido {item.OrderId} não existe.");

                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        return BadRequest($"Produto {item.ProductId} não existe.");

                    if (product.StockQuantity < item.Quantity)
                        return BadRequest($"Estoque insuficiente para o produto {product.Name}.");

                    if (product.Price != item.UnitPrice)
                        return BadRequest($"Produto {item.ProductId} com valor errado.");

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
                return Created("api/orderitems", items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar itens ao pedido");
                return StatusCode(500, "Erro interno ao processar os itens");
            }
        }

        // DELETE: api/orderitems/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOrderItem(int id)
        {
            var principal = ValidateRequestToken();

            if (principal == null)
                return Unauthorized("Token inválido ou não informado");

            try
            {
                var item = await _context.OrderItems.FindAsync(id);
                if (item == null)
                    return NotFound();

                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.StockQuantity += item.Quantity;

                var order = await _context.Orders.FindAsync(item.OrderId);
                if (order != null)
                    order.TotalAmount -= item.Quantity * item.UnitPrice;

                _context.OrderItems.Remove(item);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao remover item {id}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }
        
        private ClaimsPrincipal? ValidateRequestToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return _jwtValidator.ValidateToken(token);
        }
    }
}
