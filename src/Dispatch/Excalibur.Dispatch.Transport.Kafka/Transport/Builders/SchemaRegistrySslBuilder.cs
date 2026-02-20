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
		_options.EnableSslCertificateVerification = enable;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder CaCertificateLocation(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		_options.SslCaLocation = path;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder ClientCertificateLocation(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		_options.SslCertificateLocation = path;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder ClientKeyLocation(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		_options.SslKeyLocation = path;
		return this;
	}

	/// <inheritdoc/>
	public ISchemaRegistrySslBuilder ClientKeyPassword(string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(password);
		_options.SslKeyPassword = password;
		return this;
	}
}
