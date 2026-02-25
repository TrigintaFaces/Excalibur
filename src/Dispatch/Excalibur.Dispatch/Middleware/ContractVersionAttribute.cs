// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Attribute to specify contract version for a message type.
/// </summary>
/// <remarks> Creates a new contract version attribute. </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ContractVersionAttribute(string version) : Attribute
{
	/// <summary>
	/// Gets the contract version.
	/// </summary>
	/// <value>
	/// The contract version.
	/// </value>
	public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
}
