

using Microsoft.AspNetCore.Http;

namespace Shared.ModelDto
{
    public class ProductCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public required string Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public required IFormFile Image { get; set; }
    }
}