using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Text.Json;
using Shared.Messaging.Interfaces;
using Shared.Data;
using System.Security.Claims;
using Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace StockService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient? _rabbitMqClient; 
        private readonly ILogger<ProductController> _logger;
        private readonly IJwtTokenService _tokenValidator; 

        public ProductController(
            AppDbContext context,
            ILogger<ProductController> logger,
            IRabbitMqClient? rabbitMqClient,
            IJwtTokenService tokenValidator) 
        {
            _context = context;
            _logger = logger;
            _rabbitMqClient = rabbitMqClient;
            _tokenValidator = tokenValidator;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            try
            {
                return await _context.Products.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar produtos");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            try
            {
                var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                return product == null ? NotFound() : Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar produto {id}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Product>> PostProduct([FromBody] Product product)
        {
            try
            {
                // 🔑 Validação do token
                var principal = ValidateRequestToken();
                if (principal == null)
                    return Unauthorized("Token inválido ou não informado");
                //
                if (!ModelState.IsValid) return BadRequest(ModelState);

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                if (_rabbitMqClient != null)
                    await PublishProductEvent("product.created", product);

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"Produto {product.Id} criado pelo usuário {userId}");

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar produto");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutProduct(int id, [FromBody] Product product)
        {
            try
            {
                // // 🔑 Validação do token
                var principal = ValidateRequestToken();

                if (principal == null)
                    return Unauthorized("Token inválido ou não informado");
                //
                if (id != product.Id) return BadRequest("ID do produto não corresponde");
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null) return NotFound();

                var oldStock = existingProduct.StockQuantity;
                _context.Entry(existingProduct).CurrentValues.SetValues(product);
                await _context.SaveChangesAsync();

                if (_rabbitMqClient != null && oldStock != product.StockQuantity)
                    await PublishProductEvent("stock.updated", product);

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"Produto {id} atualizado pelo usuário {userId}");

                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, $"Conflito ao atualizar produto {id}");
                return StatusCode(409, "Conflito de concorrência detectado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao atualizar produto {id}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        private async Task PublishProductEvent(string eventType, Product product)
        {
            try
            {
                if (_rabbitMqClient == null) return;

                var message = new
                {
                    EventType = eventType,
                    ProductId = product.Id,
                    product.Name,
                    product.StockQuantity,
                    Timestamp = DateTime.UtcNow
                };

                await _rabbitMqClient.PublishAsync(JsonSerializer.Serialize(message), routingKey: eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao publicar evento {eventType} para produto {product.Id}");
            }
        }

        // 🔑 Método helper para validar token
        private ClaimsPrincipal? ValidateRequestToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return _tokenValidator.ValidateToken(token);
        }
    }
}
