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
	/// Gets or sets the SSL key location.
	/// </summary>
	/// <value>The path to the client key file, or <see langword="null"/> if not using mTLS.</value>
	public string? SslKeyLocation { get; set; }

	/// <summary>
	/// Gets or sets the SSL certificate location.
	/// </summary>
	/// <value>The path to the client certificate file, or <see langword="null"/> if not using mTLS.</value>
	public string? SslCertificateLocation { get; set; }

	/// <summary>
	/// Gets or sets the SSL key password.
	/// </summary>
	/// <value>The password for the client key, or <see langword="null"/> if unencrypted.</value>
	public string? SslKeyPassword { get; set; }
}
