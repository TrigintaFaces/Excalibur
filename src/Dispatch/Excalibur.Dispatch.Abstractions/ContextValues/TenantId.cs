// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Default tenant identifier implementation.
/// </summary>
public sealed class TenantId : ITenantId, IEquatable<TenantId>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TenantId" /> class with the specified value.
	/// </summary>
	/// <param name="value"> The tenant identifier value. </param>
	public TenantId(string? value) => Value = value ?? string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantId" /> class with an empty value.
	/// </summary>
	public TenantId() => Value = string.Empty;

	/// <inheritdoc />
	public string Value { get; set; }

	/// <summary>
	/// Implicitly converts a string to a <see cref="TenantId" />.
	/// </summary>
	/// <param name="value"> The string value to convert. </param>
	/// <returns> A new <see cref="TenantId" /> instance. </returns>
	public static implicit operator TenantId(string value) => FromString(value);

	/// <summary>
	/// Creates a new <see cref="TenantId" /> from the specified string value.
	/// </summary>
	/// <param name="value"> The string value. </param>
	/// <returns> A new <see cref="TenantId" /> instance. </returns>
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
											string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase));

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
	public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
