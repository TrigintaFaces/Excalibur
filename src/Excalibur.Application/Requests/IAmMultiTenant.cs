namespace Excalibur.Application.Requests;

/// <summary>
///     Represents an entity that is associated with a specific tenant.
/// </summary>
public interface IAmMultiTenant
{
	/// <summary>
	///     Gets the tenant identifier associated with the entity.
	/// </summary>
	string? TenantId { get; }
}
