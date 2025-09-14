using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Interface;
using Shared.Models.StockSales;
using Shared.Security.Interfaces;
using System.Security.Claims;

namespace StockService.SalesService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderItemsController : ControllerBase
    {
        private readonly IOrderItemService _orderItemService;
        private readonly ILogger<OrderItemsController> _logger;
        private readonly IJwtTokenService _jwtValidator;

        public OrderItemsController(IOrderItemService orderItemService, ILogger<OrderItemsController> logger, IJwtTokenService jwtValidator)
        {
            _orderItemService = orderItemService;
            _logger = logger;
            _jwtValidator = jwtValidator;
        }

        // ----------------------
        // GET: api/orderitems/{orderId}
        // ----------------------
        [HttpGet("{orderId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderItem>>> GetItemsByOrder(int orderId)
        {
            var principal = ValidateRequestToken();
            if (principal == null)
                return Unauthorized("Token inválido ou não informado");

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Usuário não identificado no token");

            try
            {
                var items = await _orderItemService.GetItemsByOrderAsync(orderId, userId);
                if (!items.Any())
                    return NotFound("Pedido não encontrado ou sem itens.");

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar itens do pedido {orderId}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        // ----------------------
        // POST: api/orderitems
        // ----------------------
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
                var createdItems = await _orderItemService.AddOrderItemsAsync(items);
                return Created("api/orderitems", createdItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar itens ao pedido");
                return StatusCode(500, ex.Message);
            }
        }

        // ----------------------
        // DELETE: api/orderitems/{id}
        // ----------------------
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOrderItem(int id)
        {
            var principal = ValidateRequestToken();
            if (principal == null)
                return Unauthorized("Token inválido ou não informado");

            try
            {
                await _orderItemService.DeleteOrderItemAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao remover item {id}");
                return StatusCode(500, ex.Message);
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
