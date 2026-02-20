// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Fluent builder interface for configuring Schema Registry SSL/TLS settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures mutual TLS (mTLS) authentication between the Kafka client
/// and the Schema Registry. All methods return <c>this</c> for fluent chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// kafka.UseConfluentSchemaRegistry(registry =>
/// {
///     registry.SchemaRegistryUrl("https://registry.example.com:8085")
///             .ConfigureSsl(ssl =>
///             {
///                 ssl.EnableCertificateVerification(true)
///                    .CaCertificateLocation("/path/to/ca.crt")
///                    .ClientCertificateLocation("/path/to/client.crt")
///                    .ClientKeyLocation("/path/to/client.key")
///                    .ClientKeyPassword("secret");
///             });
/// });
/// </code>
/// </example>
public interface ISchemaRegistrySslBuilder
{
	/// <summary>
	/// Enables or disables SSL certificate verification.
	/// </summary>
	/// <param name="enable">Whether to verify SSL certificates. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Only disable certificate verification in development/testing environments.
	/// Production deployments should always verify certificates.
	/// </para>
	/// </remarks>
	ISchemaRegistrySslBuilder EnableCertificateVerification(bool enable = true);

	/// <summary>
	/// Sets the location of the CA certificate for verifying the server certificate.
	/// </summary>
	/// <param name="path">The file path to the CA certificate (PEM format).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="path"/> is null or whitespace.
	/// </exception>
	ISchemaRegistrySslBuilder CaCertificateLocation(string path);

	/// <summary>
	/// Sets the location of the client certificate for mutual TLS authentication.
	/// </summary>
	/// <param name="path">The file path to the client certificate (PEM format).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="path"/> is null or whitespace.
	/// </exception>
	ISchemaRegistrySslBuilder ClientCertificateLocation(string path);

	/// <summary>
	/// Sets the location of the client private key for mutual TLS authentication.
	/// </summary>
	/// <param name="path">The file path to the client private key (PEM format).</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="path"/> is null or whitespace.
	/// </exception>
	ISchemaRegistrySslBuilder ClientKeyLocation(string path);

	/// <summary>
	/// Sets the password for the client private key, if encrypted.
	/// </summary>
	/// <param name="password">The password for the private key.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="password"/> is null or whitespace.
	/// </exception>
	ISchemaRegistrySslBuilder ClientKeyPassword(string password);
}
