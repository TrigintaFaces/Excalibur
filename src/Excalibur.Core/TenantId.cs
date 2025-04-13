namespace Excalibur.Core;

/// <summary>
///     Represents a specific implementation of <see cref="ITenantId" />.
/// </summary>
public sealed class TenantId : ITenantId, IEquatable<TenantId>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="TenantId" /> class with a specified value.
	/// </summary>
	/// <param name="value"> The tenant identifier value. </param>
	/// <remarks> If <paramref name="value" /> is <c> null </c>, the <see cref="Value" /> property is set to an empty string. </remarks>
	public TenantId(string? value) => Value = value ?? string.Empty;

	/// <summary>
	///     Initializes a new instance of the <see cref="TenantId" /> class with an empty value.
	/// </summary>
	/// <remarks>
	///     This constructor creates a <see cref="TenantId" /> instance with the <see cref="Value" /> property initialized to an empty string.
	/// </remarks>
	public TenantId() => Value = string.Empty;

	/// <inheritdoc />
	public string Value { get; set; }

	/// <inheritdoc cref="ITenantId" />
	public override string ToString() => Value;

	public bool Equals(TenantId? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return Value == other.Value;
	}

	public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is TenantId other && Equals(other));

	public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
