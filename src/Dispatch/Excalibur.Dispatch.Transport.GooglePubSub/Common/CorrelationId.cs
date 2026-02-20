// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Correlation ID for tracking requests.
/// </summary>
public sealed class CorrelationId
{
	/// <summary>
	/// Gets or sets the correlation ID value.
	/// </summary>
	/// <value>
	/// The correlation ID value.
	/// </value>
	public string Value { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Creates a new correlation ID.
	/// </summary>
	/// <returns> A new correlation ID. </returns>
	public static CorrelationId New() => new() { Value = Guid.NewGuid().ToString() };

	/// <summary>
	/// Creates a correlation ID from a string value.
	/// </summary>
	/// <param name="value"> The correlation ID value. </param>
	/// <returns> A correlation ID. </returns>
	public static CorrelationId FromString(string value) => new() { Value = value };

	/// <summary>
	/// Implicit conversion from string.
	/// </summary>
	/// <param name="value"> The string value. </param>
	/// <returns> A correlation ID. </returns>
	public static implicit operator CorrelationId(string value) => FromString(value);

	/// <summary>
	/// Implicit conversion to string.
	/// </summary>
	/// <param name="correlationId"> The correlation ID. </param>
	/// <returns> The string value. </returns>
	public static implicit operator string(CorrelationId correlationId) => correlationId.Value;

	/// <summary>
	/// Converts the correlation ID to a string.
	/// </summary>
	/// <returns> The correlation ID value. </returns>
	public override string ToString() => Value;
}
