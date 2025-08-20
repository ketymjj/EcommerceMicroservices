namespace Shared.Models
{
    public class StockHistory
    {
        // Construtor sem parâmetros necessário para EF Core
        private StockHistory() { }

        // Construtor para uso no código
        public StockHistory(int productId, string changedBy)
        {
            ProductId = productId;
            ChangedBy = changedBy ?? throw new ArgumentNullException(nameof(changedBy));
        }

        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }  // navegação
        public string? ChangedBy { get; set; }
        public int OldQuantity { get; set; }
        public int NewQuantity { get; set; }
        public int QuantityDelta => NewQuantity - OldQuantity;
        public DateTime ChangedAt { get; set; }
    }
}
