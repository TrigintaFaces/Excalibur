// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// SSL/TLS configuration options for the Confluent Schema Registry connection.
/// </summary>
/// <remarks>
/// This sub-options class is part of the <see cref="ConfluentSchemaRegistryOptions"/> ISP split
/// to keep each class within the 10-property gate.
/// </remarks>
public sealed class SchemaRegistrySslOptions
{
	/// <summary>
	/// Gets or sets whether to enable SSL verification.
	/// </summary>
	/// <value><see langword="true"/> to verify SSL certificates; otherwise, <see langword="false"/>.</value>
	public bool EnableSslCertificateVerification { get; set; } = true;

	/// <summary>
	/// Gets or sets the SSL CA certificate location.
	/// </summary>
	/// <value>The path to the CA certificate file, or <see langword="null"/> to use system certificates.</value>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// Gets or sets the client keystore presented for mutual TLS.
	/// </summary>
	/// <value>
	/// The path to a PKCS#12 (<c>.p12</c>/<c>.pfx</c>) keystore holding the client certificate and its
	/// private key, or <see langword="null"/> when not using mTLS.
	/// </value>
	/// <remarks>
	/// A keystore rather than a certificate/key pair because that is the only client-credential shape the
	/// Schema Registry client accepts. The broker client takes separate PEM files; the registry client does
	/// not, and a certificate and key configured as separate paths cannot be presented to the registry at
	/// all — they would read as mutual TLS and produce a one-way TLS connection.
	/// </remarks>
	public string? SslKeystoreLocation { get; set; }

	/// <summary>
	/// Gets or sets the password protecting <see cref="SslKeystoreLocation"/>.
	/// </summary>
	/// <value>The keystore password, or <see langword="null"/> when no keystore is configured.</value>
	public string? SslKeystorePassword { get; set; }
}
