// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.Serialization;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Exception thrown when no fallback is available.
/// </summary>
[Serializable]
public class NoFallbackAvailableException : Exception
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

	// R0.8: Suppress SYSLIB0051 - Serialization constructor required for remoting scenarios and backward compatibility
#pragma warning disable SYSLIB0051
	/// <summary>
	/// Initializes a new instance of the <see cref="NoFallbackAvailableException"/> class with serialized data.
	/// </summary>
	/// <param name="info">The serialization info.</param>
	/// <param name="context">The streaming context.</param>
	protected NoFallbackAvailableException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
#pragma warning restore SYSLIB0051
}
