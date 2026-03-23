// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Exception thrown when no fallback is available.
/// </summary>
public sealed class NoFallbackAvailableException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="NoFallbackAvailableException"/> class.
	/// </summary>
	public NoFallbackAvailableException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="NoFallbackAvailableException" /> class.
	/// </summary>
	/// <param name="message"> The exception message. </param>
	public NoFallbackAvailableException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="NoFallbackAvailableException" /> class.
	/// </summary>
	/// <param name="message"> The exception message. </param>
	/// <param name="innerException"> The inner exception. </param>
	public NoFallbackAvailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
