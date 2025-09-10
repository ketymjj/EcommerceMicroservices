using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Shared.Messaging.Interfaces;
using Shared.Data;
using System.Security.Claims;
using Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Shared.Models.StockSales;
using StockService.ModelDto;

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
       public async Task<ActionResult<Product>> PostProduct([FromForm] ProductCreateDto dto)
       {
           try
           {
               var principal = ValidateRequestToken();
               if (principal == null)
                   return Unauthorized("Token inválido ou não informado");
       
               if (!ModelState.IsValid) return BadRequest(ModelState);
       
               // Salvar imagem no servidor
               string? imagePath = null;
               if (dto.Image != null && dto.Image.Length > 0)
               {
                   var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                   if (!Directory.Exists(uploadsFolder))
                       Directory.CreateDirectory(uploadsFolder);
       
                   var fileName = Guid.NewGuid() + Path.GetExtension(dto.Image.FileName);
                   var filePath = Path.Combine(uploadsFolder, fileName);
       
                   using (var stream = new FileStream(filePath, FileMode.Create))
                   {
                       await dto.Image.CopyToAsync(stream);
                   }
       
                   imagePath = "/images/" + fileName; // URL relativa
               }
       
               var product = new Product
               {
                   Name = dto.Name,
                   Description = dto.Description,
                   Price = dto.Price,
                   StockQuantity = dto.StockQuantity,
                   ImageUrl = imagePath // precisa ter essa coluna na tabela Product
               };
       
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
        public async Task<IActionResult> PutProduct(int id, [FromForm] ProductCreateDto dto)
        {
            try
            {
                var principal = ValidateRequestToken();
                if (principal == null)
                    return Unauthorized("Token inválido ou não informado");
        
                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null) return NotFound();
        
                // Atualizar dados
                existingProduct.Name = dto.Name;
                existingProduct.Price = dto.Price;
                existingProduct.StockQuantity = dto.StockQuantity;
                existingProduct.UpdatedAt = DateTime.UtcNow;
        
                // Se enviou nova imagem, sobrescreve
                if (dto.Image != null && dto.Image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var fileName = Guid.NewGuid() + Path.GetExtension(dto.Image.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await dto.Image.CopyToAsync(stream);
                    }

                    existingProduct.ImageUrl = "/images/" + fileName;
                }
        
                await _context.SaveChangesAsync();
        
                if (_rabbitMqClient != null)
                    await PublishProductEvent("product.updated", existingProduct);
        
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"Produto {id} atualizado pelo usuário {userId}");
        
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao atualizar produto {id}");
                return StatusCode(500, "Erro interno ao processar a requisição");
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var principal = ValidateRequestToken();
                if (principal == null)
                    return Unauthorized("Token inválido ou não informado");
        
                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null) return NotFound();
        
                // Se tiver imagem salva, apaga do disco também
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    var imagePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        existingProduct.ImageUrl.TrimStart('/')
                    );
        
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
        
                _context.Products.Remove(existingProduct);
                await _context.SaveChangesAsync();
        
                if (_rabbitMqClient != null)
                    await PublishProductEvent("product.deleted", existingProduct);
        
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"Produto {id} deletado pelo usuário {userId}");
        
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao deletar produto {id}");
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
