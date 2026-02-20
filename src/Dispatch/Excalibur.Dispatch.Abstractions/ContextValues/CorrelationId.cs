// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Default implementation of <see cref="ICorrelationId" />.
/// </summary>
public sealed class CorrelationId : ICorrelationId, IEquatable<CorrelationId>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CorrelationId" /> class with the specified GUID value.
	/// </summary>
	/// <param name="value"> The GUID value for the correlation identifier. </param>
	public CorrelationId(Guid value) => Value = value;

	/// <summary>
	/// Initializes a new instance of the <see cref="CorrelationId" /> class with the specified string value.
	/// </summary>
	/// <param name="value"> The string representation of the GUID value. </param>
	/// <exception cref="ArgumentException"> Thrown when the value is null or whitespace. </exception>
	/// <exception cref="FormatException"> Thrown when the value cannot be parsed as a GUID. </exception>
	public CorrelationId(string? value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Value = Guid.Parse(value);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CorrelationId" /> class with a new GUID value.
	/// </summary>
	public CorrelationId() => Value = Guid.NewGuid();

	/// <inheritdoc />
	public Guid Value { get; set; }

	/// <inheritdoc cref="Guid" />
	public override string ToString() => Value.ToString();

	/// <summary>
	/// Determines whether the specified <see cref="CorrelationId" /> is equal to the current instance.
	/// </summary>
	/// <param name="other"> The <see cref="CorrelationId" /> to compare. </param>
	/// <returns> true if the specified <see cref="CorrelationId" /> is equal to the current instance; otherwise, false. </returns>
	public bool Equals(CorrelationId? other) => other is not null && (ReferenceEquals(this, other) || Value == other.Value);

	/// <summary>
	/// Determines whether the specified object is equal to the current instance.
	/// </summary>
	/// <param name="obj"> The object to compare. </param>
	/// <returns> true if the specified object is equal to the current instance; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is CorrelationId other && Equals(other);

	/// <summary>
	/// Returns the hash code for this instance.
	/// </summary>
	/// <returns> A 32-bit signed integer hash code. </returns>
	public override int GetHashCode() => Value.GetHashCode();
}
