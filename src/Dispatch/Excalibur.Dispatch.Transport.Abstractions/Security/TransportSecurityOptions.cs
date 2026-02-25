// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Configuration options for transport security.
/// </summary>
/// <remarks>
/// <para>
/// Transport security is enforced at connection time rather than per-message to minimize overhead.
/// </para>
/// </remarks>
public class TransportSecurityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether TLS is required for the transport connection.
	/// </summary>
	/// <value>True to require TLS; false to allow non-TLS connections. Default is true.</value>
	/// <remarks>
	/// <para>
	/// When set to true (default), the transport will throw a <see cref="TransportSecurityException"/>
	/// at connection time if TLS cannot be verified.
	/// </para>
	/// <para>
	/// <strong>SECURITY WARNING:</strong> Setting this to false allows plaintext communication
	/// which may expose sensitive data. Only disable for development/testing environments.
	/// </para>
	/// </remarks>
	public bool RequireTls { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate the server certificate.
	/// </summary>
	/// <value>True to validate server certificates; false to skip validation. Default is true.</value>
	/// <remarks>
	/// <para>
	/// <strong>SECURITY WARNING:</strong> Disabling certificate validation makes the connection
	/// vulnerable to man-in-the-middle attacks. Only disable for development/testing environments.
	/// </para>
	/// </remarks>
	public bool ValidateServerCertificate { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum TLS version allowed for connections.
	/// </summary>
	/// <value>The minimum TLS version. Default is TLS 1.2.</value>
	/// <remarks>
	/// TLS 1.2 is the minimum recommended version for compliance with most security standards.
	/// TLS 1.3 provides additional security improvements but may not be supported by all brokers.
	/// </remarks>
	public TlsVersion MinimumTlsVersion { get; set; } = TlsVersion.Tls12;

	/// <summary>
	/// Gets or sets the path to the client certificate for mutual TLS (mTLS) authentication.
	/// </summary>
	/// <value>The path to the client certificate, or null if mTLS is not used.</value>
	public string? ClientCertificatePath { get; set; }

	/// <summary>
	/// Gets or sets the path to the client certificate private key.
	/// </summary>
	/// <value>The path to the private key, or null if mTLS is not used.</value>
	public string? ClientCertificateKeyPath { get; set; }

	/// <summary>
	/// Gets or sets the password for the client certificate private key.
	/// </summary>
	/// <value>The password, or null if the key is not password-protected.</value>
	public string? ClientCertificatePassword { get; set; }
}

/// <summary>
/// Specifies the TLS protocol version.
/// </summary>
public enum TlsVersion
{
	/// <summary>
	/// TLS 1.0 - Deprecated, not recommended for use.
	/// </summary>
	Tls10 = 0,

	/// <summary>
	/// TLS 1.1 - Deprecated, not recommended for use.
	/// </summary>
	Tls11 = 1,

	/// <summary>
	/// TLS 1.2 - Minimum recommended version for most compliance standards.
	/// </summary>
	Tls12 = 2,

	/// <summary>
	/// TLS 1.3 - Latest version with improved security.
	/// </summary>
	Tls13 = 3
}
