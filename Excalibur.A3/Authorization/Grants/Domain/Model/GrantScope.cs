namespace Excalibur.A3.Authorization.Grants.Domain.Model;

/// <summary>
///     Represents the scope of a grant, including tenant ID, grant type, and qualifier.
/// </summary>
/// <param name="TenantId"> The tenant ID associated with the scope. </param>
/// <param name="GrantType"> The type of the grant. </param>
/// <param name="Qualifier"> A qualifier providing additional context for the grant type. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null or empty. </exception>
public record GrantScope(string TenantId, string GrantType, string Qualifier)
{
	/// <summary>
	///     Gets or sets the tenant ID associated with the scope.
	/// </summary>
	public string TenantId { get; set; } = !string.IsNullOrEmpty(TenantId) ? TenantId : throw new ArgumentNullException(TenantId);

	/// <summary>
	///     Gets or sets the type of the grant.
	/// </summary>
	public string GrantType { get; set; } = !string.IsNullOrEmpty(GrantType) ? GrantType : throw new ArgumentNullException(GrantType);

	/// <summary>
	///     Gets or sets the qualifier providing additional context for the grant type.
	/// </summary>
	public string Qualifier { get; set; } = !string.IsNullOrEmpty(Qualifier) ? Qualifier : throw new ArgumentNullException(Qualifier);

	/// <summary>
	///     Creates a <see cref="GrantScope" /> instance from a string in the format '[TenantId]:[GrantType]:[Qualifier]'.
	/// </summary>
	/// <param name="scope"> The scope string to parse. </param>
	/// <returns> A new <see cref="GrantScope" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="scope" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown if the format of <paramref name="scope" /> is invalid. </exception>
	public static GrantScope FromString(string scope)
	{
		ArgumentNullException.ThrowIfNull(scope);

		var parts = scope.Split(':', 3, StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length != 3)
		{
			throw new ArgumentException("The scope is invalid. The expected format is '[TenantId]:[GrantType]:[Qualifier]'");
		}

		return new GrantScope(parts[0], parts[1], parts[2]);
	}

	/// <summary>
	///     Returns a string representation of the scope in the format '[TenantId]:[GrantType]:[Qualifier]'.
	/// </summary>
	/// <returns> A string representation of the scope. </returns>
	public override string ToString() => $"{TenantId}:{GrantType}:{Qualifier}";
}
