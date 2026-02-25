// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Exception thrown for contract version Handlers.TestInfrastructure.
/// </summary>
public sealed class ContractVersionException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ContractVersionException"/> class.
	/// Creates a new contract version exception.
	/// </summary>
	public ContractVersionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContractVersionException"/> class.
	/// Creates a new contract version exception with inner exception.
	/// </summary>
	public ContractVersionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContractVersionException"/> class.
	/// </summary>
	public ContractVersionException() : base()
	{
	}
}
