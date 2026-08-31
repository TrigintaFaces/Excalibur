// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Exception thrown when a transport security requirement is not met.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown before a connection carries any message, when:
/// </para>
/// <list type="bullet">
/// <item><description>
/// TLS is required but the connection is not secure. <see cref="FailureReason"/> is
/// <see cref="TransportSecurityFailureReason.TlsNotEnabled"/>.
/// </description></item>
/// <item><description>
/// A transport's security protocol is configured in two places that disagree, or with a value the
/// transport does not recognize. Refusing beats guessing at a security control, so the connection is
/// never established. <see cref="FailureReason"/> is
/// <see cref="TransportSecurityFailureReason.Unspecified"/>, because the failure is a configuration
/// contradiction rather than a wire-security posture; the message names the conflicting settings.
/// </description></item>
/// </list>
/// </remarks>
public sealed class TransportSecurityException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSecurityException"/> class.
	/// </summary>
	public TransportSecurityException()
		: base("Transport security requirements were not met.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSecurityException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public TransportSecurityException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSecurityException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public TransportSecurityException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the transport where the security failure occurred, such as <c>Kafka</c> or <c>RabbitMQ</c>.
	/// </summary>
	/// <remarks>
	/// This identifies the transport family that refused the connection, not the name a connection was
	/// registered under in dependency injection. A host wiring more than one transport uses it to tell
	/// which one refused.
	/// </remarks>
	public string? TransportName { get; init; }

	/// <summary>
	/// Gets the specific security failure reason, for callers that branch on the failure rather than
	/// surface its message.
	/// </summary>
	/// <remarks>
	/// <see cref="TransportSecurityFailureReason.Unspecified"/> is a real outcome and not a missing
	/// value: it means the refusal was not a wire-security posture failure. Read
	/// <see cref="Exception.Message"/> for those.
	/// </remarks>
	public TransportSecurityFailureReason FailureReason { get; init; }
}

/// <summary>
/// Specifies the reason for a transport security failure.
/// </summary>
/// <remarks>
/// The numbering is not contiguous. Values are never reassigned, because a stored or logged number
/// would silently come to mean something else.
/// </remarks>
public enum TransportSecurityFailureReason
{
	/// <summary>
	/// The failure reason is not specified.
	/// </summary>
	Unspecified = 0,

	/// <summary>
	/// TLS is required but the connection does not carry it, so credentials and message payloads would
	/// travel in the clear.
	/// </summary>
	TlsNotEnabled = 1,
}
