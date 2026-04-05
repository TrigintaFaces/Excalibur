namespace Testing.ProductionCode;

/// <summary>
/// Simple repository interface for order persistence.
/// In production this would be backed by a database; in tests we fake it.
/// </summary>
public interface IOrderRepository
{
    Task<string> SaveAsync(string productName, int quantity, CancellationToken cancellationToken);
}
