// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Concrete tenant identifier message value-type.
/// </summary>
/// <remarks>
/// <para>
/// The identifier is validated once, at construction, and is immutable thereafter. Those two properties are
/// what make a tenant check meaningful: a value that could be changed after it was authorised would let a
/// caller pass a scope check and then operate as a different tenant, and a value that silently substituted
/// something for a missing identifier would make "no tenant was supplied" indistinguishable from a real
/// scope. Both are refused here rather than normalised away.
/// </para>
/// <para>
/// Comparison is ordinal and case-sensitive, matching the tenant term used by the scoping types and by the
/// storage predicates built from them. Two identifiers differing only in case are two different tenants.
/// </para>
/// </remarks>
public sealed class TenantId : IEquatable<TenantId>
{
	/// <summary>
	/// The longest tenant identifier every shipped provider is guaranteed to store whole.
	/// </summary>
	/// <remarks>
	/// Fixed at the <strong>narrowest</strong> shipped tenant column across every provider, not the widest:
	/// a caller-supplied identifier this constructor accepts must never be silently truncated by any
	/// provider it can reach, and truncation is the dangerous outcome — a truncated identifier can collide
	/// with another tenant's. Rejecting at construction, where the caller still has context, is cheaper
	/// than discovering the mismatch as a provider-specific truncation or constraint error far from the
	/// call that caused it.
	/// </remarks>
	public const int MaxLength = 64;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantId" /> class with the specified value.
	/// </summary>
	/// <param name="value"> The tenant identifier value. </param>
	/// <exception cref="ArgumentException">
	/// <paramref name="value" /> is <see langword="null" />, empty, or whitespace — a missing tenant is
	/// rejected rather than coerced: substituting an empty value would produce an identifier that no longer
	/// names the tenant the caller intended, with no diagnostic at the point the mistake was made — or
	/// longer than <see cref="MaxLength"/> characters, which no shipped provider can store whole.
	/// </exception>
	public TenantId(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		if (value.Length > MaxLength)
		{
			throw new ArgumentException(
				$"Tenant identifier exceeds the maximum length of {MaxLength} characters supported by every shipped provider.",
				nameof(value));
		}

		Value = value;
	}

	/// <summary>
	/// Gets the tenant identifier value. Never <see langword="null" />, empty, or whitespace.
	/// </summary>
	/// <value> The unique tenant identifier string. </value>
	public string Value { get; }

	/// <summary>
	/// Creates a new <see cref="TenantId" /> from the specified string value.
	/// </summary>
	/// <param name="value"> The string value. </param>
	/// <returns> A new <see cref="TenantId" /> instance. </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="value" /> is <see langword="null" />, empty, whitespace, or longer than
	/// <see cref="MaxLength"/> characters.
	/// </exception>
	public static TenantId FromString(string value) => new(value);

	/// <inheritdoc cref="string" />
	public override string ToString() => Value;

	/// <summary>
	/// Determines whether the specified <see cref="TenantId" /> is equal to the current instance.
	/// </summary>
	/// <param name="other"> The <see cref="TenantId" /> to compare. </param>
	/// <returns> true if the specified <see cref="TenantId" /> is equal to the current instance; otherwise, false. </returns>
	public bool Equals(TenantId? other) => other is not null &&
										   (ReferenceEquals(this, other) ||
											string.Equals(Value, other.Value, StringComparison.Ordinal));

	/// <summary>
	/// Determines whether the specified object is equal to the current instance.
	/// </summary>
	/// <param name="obj"> The object to compare. </param>
	/// <returns> true if the specified object is equal to the current instance; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is TenantId other && Equals(other);

	/// <summary>
	/// Returns the hash code for this instance.
	/// </summary>
	/// <returns> A 32-bit signed integer hash code. </returns>
	public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
}
