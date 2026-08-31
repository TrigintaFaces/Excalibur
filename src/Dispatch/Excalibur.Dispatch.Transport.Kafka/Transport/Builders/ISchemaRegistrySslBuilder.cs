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
///                    .ClientKeystore("/path/to/client.p12", keystorePassword);
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
	/// Sets the client keystore presented to the Schema Registry for mutual TLS authentication.
	/// </summary>
	/// <param name="path">
	/// The file path to a PKCS#12 (<c>.p12</c>/<c>.pfx</c>) keystore holding the client certificate and
	/// its private key.
	/// </param>
	/// <param name="password">The password protecting the keystore.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="path"/> or <paramref name="password"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// A keystore, not a certificate/key pair: the Schema Registry client accepts client credentials only
	/// in that form. Convert an existing PEM pair with
	/// <c>openssl pkcs12 -export -in client.crt -inkey client.key -out client.p12</c>.
	/// </para>
	/// <para>
	/// <b>What this defends and what it does not.</b> It authenticates this client TO the registry, so a
	/// registry configured to require client certificates will reject callers that present none. It says
	/// nothing about the Kafka broker connection, which carries its own posture, and nothing about the
	/// authenticity of the registry itself — that is
	/// <see cref="EnableCertificateVerification"/> together with <see cref="CaCertificateLocation"/>,
	/// and disabling verification leaves the connection encrypted but unauthenticated.
	/// </para>
	/// </remarks>
	ISchemaRegistrySslBuilder ClientKeystore(string path, string password);
}
