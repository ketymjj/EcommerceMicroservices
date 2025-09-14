using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.ModelDto;
using Shared.Models.StockSales;

namespace Shared.Interface
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product> CreateProductAsync(ProductCreateDto dto, string userId);
        Task<Product?> UpdateProductAsync(int id, ProductCreateDto dto, string userId);
        Task<bool> DeleteProductAsync(int id, string userId);
    }
}