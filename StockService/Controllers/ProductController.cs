using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Security.Interfaces;
using Shared.ModelDto;
using Shared.Interface;

namespace StockService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IJwtTokenService _tokenValidator;

        public ProductController(IProductService productService, IJwtTokenService tokenValidator)
        {
            _productService = productService;
            _tokenValidator = tokenValidator;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _productService.GetProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            return product == null ? NotFound() : Ok(product);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostProduct([FromForm] ProductCreateDto dto)
        {
            var principal = ValidateRequestToken();
            if (principal == null) return Unauthorized("Token inválido ou não informado");

            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "desconhecido";

            var product = await _productService.CreateProductAsync(dto, userId);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutProduct(int id, [FromForm] ProductCreateDto dto)
        {
            var principal = ValidateRequestToken();
            if (principal == null) return Unauthorized("Token inválido ou não informado");

            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "desconhecido";

            var updated = await _productService.UpdateProductAsync(id, dto, userId);
            return updated == null ? NotFound() : NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var principal = ValidateRequestToken();
            if (principal == null) return Unauthorized("Token inválido ou não informado");

            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "desconhecido";

            var deleted = await _productService.DeleteProductAsync(id, userId);
            return deleted ? NoContent() : NotFound();
        }

        private System.Security.Claims.ClaimsPrincipal? ValidateRequestToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return _tokenValidator.ValidateToken(token);
        }
    }
}
