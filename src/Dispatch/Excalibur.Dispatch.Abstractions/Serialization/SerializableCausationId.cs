// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Serializable implementation of ICausationId for AOT compatibility.
/// </summary>
public sealed class SerializableCausationId : ICausationId
{
	/// <inheritdoc />
	public Guid Value { get; set; }

	/// <summary>
	/// Creates a new serializable causation ID.
	/// </summary>
	/// <param name="value"> The optional GUID value for the causation ID; if not provided, a new GUID will be generated. </param>
	public static SerializableCausationId Create(Guid? value = null)
		=> new() { Value = value ?? Guid.NewGuid() };

	/// <inheritdoc />
	public override string ToString() => Value.ToString();
}
