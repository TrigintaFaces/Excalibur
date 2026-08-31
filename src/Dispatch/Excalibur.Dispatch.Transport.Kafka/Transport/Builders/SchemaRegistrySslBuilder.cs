// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Internal implementation of the Schema Registry SSL builder.
/// </summary>
internal sealed class SchemaRegistrySslBuilder : ISchemaRegistrySslBuilder
{
	private readonly ConfluentSchemaRegistryOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRegistrySslBuilder"/> class.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	public SchemaRegistrySslBuilder(ConfluentSchemaRegistryOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder EnableCertificateVerification(bool enable = true)
	{
		_options.Ssl.EnableSslCertificateVerification = enable;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder CaCertificateLocation(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		_options.Ssl.SslCaLocation = path;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder ClientKeystore(string path, string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentException.ThrowIfNullOrWhiteSpace(password);

		// Both together, deliberately: a keystore path with no password cannot be opened, so accepting one
		// without the other would store a credential that can never be presented.
		_options.Ssl.SslKeystoreLocation = path;
		_options.Ssl.SslKeystorePassword = password;
		return this;
	}
}
