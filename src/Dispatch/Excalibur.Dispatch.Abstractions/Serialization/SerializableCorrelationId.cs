// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Serializable implementation of ICorrelationId for AOT compatibility.
/// </summary>
public sealed class SerializableCorrelationId : ICorrelationId
{
	/// <summary>
	/// Gets or sets the correlation ID value.
	/// </summary>
	/// <value> The current <see cref="Value" /> value. </value>
	public Guid Value { get; set; }

	/// <summary>
	/// Gets the correlation identifier as a string.
	/// </summary>
	public string CorrelationId => Value.ToString();

	/// <summary>
	/// Creates a new serializable correlation ID.
	/// </summary>
	/// <param name="value"> The optional GUID value for the correlation ID; if not provided, a new GUID will be generated. </param>
	public static SerializableCorrelationId Create(Guid? value = null)
		=> new() { Value = value ?? Guid.NewGuid() };

	/// <inheritdoc />
	public override string ToString() => Value.ToString();
}
