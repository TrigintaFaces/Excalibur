// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.SchemaRegistry;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Locks the Schema Registry client credentials against the artifact the client is actually constructed
/// with -- the <see cref="SchemaRegistryConfig"/> -- rather than against the option that holds them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix.</b> Three public builder methods wrote a client certificate, key and key password into
/// options that the client never read, so a consumer configured mutual TLS and got one-way TLS with the
/// client certificate never presented. It survived because the existing tests asserted the properties
/// store what you set -- which they did. That is not the load-bearing property; reaching the client is.
/// Every arm here therefore reads <see cref="ConfluentSchemaRegistryClient.BuildConfig"/>, and no arm
/// asserts a setter round-trip.
/// </para>
/// <para>
/// <b>The credentials were also the wrong shape.</b> <see cref="SchemaRegistryConfig"/> takes a PKCS#12
/// keystore (<c>SslKeystoreLocation</c>/<c>SslKeystorePassword</c>); the separate PEM certificate/key/
/// password triple belongs to the broker client and has no counterpart on the registry client, so those
/// three values could not have been forwarded even if someone had tried.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Transport", "Kafka")]
public sealed class ConfluentSchemaRegistryClientConfigShould
{
	/// <summary>
	/// The defect. A configured client keystore must reach the client, or mutual TLS silently is not.
	/// </summary>
	[Fact]
	public void CarryTheClientKeystoreToTheClientConfiguration()
	{
		var options = Options();
		_ = new SchemaRegistrySslBuilder(options).ClientKeystore("/certs/client.p12", "secret");

		var config = ConfluentSchemaRegistryClient.BuildConfig(options);

		config.SslKeystoreLocation.ShouldBe(
			"/certs/client.p12",
			"a keystore the consumer configured but the client never receives is mutual TLS in the "
			+ "configuration and one-way TLS on the wire.");
		config.SslKeystorePassword.ShouldBe("secret", "a keystore that cannot be opened cannot be presented.");
	}

	/// <summary>
	/// Liveness. Without this, assigning the keystore fields unconditionally would satisfy the arm above
	/// while telling the SDK to open a keystore that does not exist.
	/// </summary>
	[Fact]
	public void LeaveTheClientKeystoreUnsetWhenNoneIsConfigured()
	{
		var config = ConfluentSchemaRegistryClient.BuildConfig(Options());

		config.SslKeystoreLocation.ShouldBeNull();
		config.SslKeystorePassword.ShouldBeNull();
	}

	/// <summary>
	/// Liveness for the rest of the translation, so an arm above cannot pass over a config that carries
	/// nothing else either.
	/// </summary>
	[Fact]
	public void CarryTheServerTrustSettingsToTheClientConfiguration()
	{
		var options = Options();
		options.Ssl.SslCaLocation = "/certs/ca.crt";
		options.Ssl.EnableSslCertificateVerification = true;
		options.BasicAuthUserInfo = "user:pass";

		var config = ConfluentSchemaRegistryClient.BuildConfig(options);

		config.Url.ShouldBe(options.Url);
		config.SslCaLocation.ShouldBe("/certs/ca.crt");
		config.EnableSslCertificateVerification.ShouldBe(true);
		config.BasicAuthUserInfo.ShouldBe("user:pass");
	}

	/// <summary>
	/// Refusal. A keystore path with no password is a credential that can never be presented, so the
	/// builder rejects it rather than storing half of one.
	/// </summary>
	[Theory]
	[InlineData("/certs/client.p12", "")]
	[InlineData("/certs/client.p12", "   ")]
	[InlineData("", "secret")]
	public void RefuseAnIncompleteClientKeystore(string path, string password)
	{
		var ssl = new SchemaRegistrySslBuilder(Options());

		_ = Should.Throw<ArgumentException>(() => ssl.ClientKeystore(path, password));
	}

	private static ConfluentSchemaRegistryOptions Options() =>
		new() { Url = "https://registry.internal:8085" };
}
