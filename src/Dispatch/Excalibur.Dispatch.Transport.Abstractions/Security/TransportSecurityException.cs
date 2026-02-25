// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Exception thrown when a transport security requirement is not met.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown at connection time when:
/// </para>
/// <list type="bullet">
/// <item><description>TLS is required but the connection is not secure</description></item>
/// <item><description>Server certificate validation fails</description></item>
/// <item><description>The TLS version is below the minimum required</description></item>
/// <item><description>Mutual TLS (mTLS) authentication fails</description></item>
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
	/// Gets or sets the transport name where the security failure occurred.
	/// </summary>
	public string? TransportName { get; init; }

	/// <summary>
	/// Gets or sets the specific security failure reason.
	/// </summary>
	public TransportSecurityFailureReason FailureReason { get; init; }
}

/// <summary>
/// Specifies the reason for a transport security failure.
/// </summary>
public enum TransportSecurityFailureReason
{
	/// <summary>
	/// The failure reason is not specified.
	/// </summary>
	Unspecified = 0,

	/// <summary>
	/// TLS is not enabled on the connection.
	/// </summary>
	TlsNotEnabled = 1,

	/// <summary>
	/// The TLS version is below the minimum required.
	/// </summary>
	TlsVersionTooLow = 2,

	/// <summary>
	/// Server certificate validation failed.
	/// </summary>
	CertificateValidationFailed = 3,

	/// <summary>
	/// Client certificate (mTLS) authentication failed.
	/// </summary>
	ClientCertificateFailed = 4,

	/// <summary>
	/// The connection was downgraded from TLS to plaintext.
	/// </summary>
	ConnectionDowngraded = 5
}
