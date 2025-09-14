using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging.Interfaces;
using Shared.Models.StockSales;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Interface;
using Shared.ModelDto;

namespace StockService.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<ProductService> _logger;

        public ProductService(AppDbContext context, IRabbitMqClient rabbitMqClient, ILogger<ProductService> logger)
        {
            _context = context;
            _rabbitMqClient = rabbitMqClient;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            return await _context.Products.AsNoTracking().ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Product> CreateProductAsync(ProductCreateDto dto, string userId)
        {
            string? imagePath = await SaveImageAsync(dto.Image);

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                ImageUrl = imagePath
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await PublishProductEvent("product.created", product);

            _logger.LogInformation($"Produto {product.Id} criado pelo usuário {userId}");
            return product;
        }

        public async Task<Product?> UpdateProductAsync(int id, ProductCreateDto dto, string userId)
        {
            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null) return null;

            existingProduct.Name = dto.Name;
            existingProduct.Price = dto.Price;
            existingProduct.StockQuantity = dto.StockQuantity;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            if (dto.Image != null && dto.Image.Length > 0)
                existingProduct.ImageUrl = await SaveImageAsync(dto.Image);

            await _context.SaveChangesAsync();

            await PublishProductEvent("product.updated", existingProduct);

            _logger.LogInformation($"Produto {id} atualizado pelo usuário {userId}");
            return existingProduct;
        }

        public async Task<bool> DeleteProductAsync(int id, string userId)
        {
            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null) return false;

            if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
            {
                var imagePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    existingProduct.ImageUrl.TrimStart('/')
                );

                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }

            _context.Products.Remove(existingProduct);
            await _context.SaveChangesAsync();

            await PublishProductEvent("product.deleted", existingProduct);

            _logger.LogInformation($"Produto {id} deletado pelo usuário {userId}");
            return true;
        }

        private async Task<string?> SaveImageAsync(IFormFile? image)
        {
            if (image == null || image.Length == 0) return null;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return "/images/" + fileName;
        }

        private async Task PublishProductEvent(string eventType, Product product)
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

            await _rabbitMqClient.PublishAsync(System.Text.Json.JsonSerializer.Serialize(message), routingKey: eventType);
        }
    }
}
