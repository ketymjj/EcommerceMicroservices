using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging.Interfaces;
using System.Text.Json;
using Shared.Data;
using Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Shared.Models.StockSales;

namespace StockService.SalesService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<OrdersController> _logger;
        private readonly IJwtTokenService _jwtValidator;

        public OrdersController(
            AppDbContext context,
            IRabbitMqClient rabbitMqClient,
            ILogger<OrdersController> logger,
            IJwtTokenService jwtValidator)
        {
            _context = context;
            _rabbitMqClient = rabbitMqClient;
            _logger = logger;
            _jwtValidator = jwtValidator;
        }

       [HttpGet]
       [Authorize]
       [ProducesResponseType(StatusCodes.Status200OK)]
       [ProducesResponseType(StatusCodes.Status401Unauthorized)]
       [ProducesResponseType(StatusCodes.Status500InternalServerError)]
       public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
       {
           var principal = ValidateRequestToken();
       
           if (principal == null)
               return Unauthorized("Token inválido ou não informado");
       
           var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (string.IsNullOrEmpty(userId))
               return Unauthorized("Usuário não identificado no token");
       
           try
           {
               var orders = await _context.Orders
                   .AsNoTracking()
                   .Where(o => o.CustomerId == userId) // 🔑 filtra pelo usuário logado
                   .OrderByDescending(o => o.OrderDate) // 🔑 ordena pela data
                   .ToListAsync();
       
               return Ok(orders);
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Erro ao buscar pedidos do usuário {userId}");
               return StatusCode(500, "Erro interno ao processar a requisição");
           }
       }


        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
           var principal = ValidateRequestToken();

                if (principal == null)
                    return Unauthorized("Token inválido ou não informado");
       
        try
            {
                var order = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id);

                return order == null ? NotFound() : Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar pedido {id}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Order>> PostOrder([FromBody] Order order)
        {
            var principal = ValidateRequestToken();
            if (principal == null)
                return Unauthorized("Token inválido ou não informado");
        
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
        
                // Criar pedido no banco
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
        
                // Retorna o pedido salvo, com ID
                return CreatedAtAction(nameof(GetOrder), new { id = orderToSave.Id }, orderToSave);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar pedido");
                return StatusCode(500, "Erro interno ao processar o pedido");
            }
        }

        
          // 🔑 Método helper para validar token
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
