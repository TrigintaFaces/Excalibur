// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Serializable, concrete tenant identifier value-type for AOT compatibility.
/// </summary>
public sealed class SerializableTenantId
{
	private string _value = string.Empty;

	/// <summary>
	/// Gets or sets the tenant identifier value.
	/// </summary>
	/// <value> The unique tenant identifier string. </value>
	public string Value
	{
		get => _value;
		set => _value = value ?? string.Empty;
	}

	/// <summary>
	/// Creates a new serializable tenant ID.
	/// </summary>
	/// <param name="value"> The optional tenant ID value; if not provided, an empty string will be used. </param>
	public static SerializableTenantId Create(string? value = null)
		=> new() { Value = value ?? string.Empty };

	/// <summary>
	/// Returns the tenant identifier as a string.
	/// </summary>
	/// <returns> The tenant identifier value. </returns>
	public override string ToString() => Value;
}
