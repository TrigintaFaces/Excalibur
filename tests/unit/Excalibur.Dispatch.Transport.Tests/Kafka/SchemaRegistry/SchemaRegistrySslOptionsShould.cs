// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for <see cref="SchemaRegistrySslOptions"/>.
/// Verifies defaults and property assignment for SSL-level settings.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class SchemaRegistrySslOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new SchemaRegistrySslOptions();

		// Assert
		options.EnableSslCertificateVerification.ShouldBeTrue();
		options.SslCaLocation.ShouldBeNull();
		options.SslKeystoreLocation.ShouldBeNull();
		options.SslKeystorePassword.ShouldBeNull();
	}

	[Fact]
	public void AllowDisablingSslVerification()
	{
		// Arrange & Act
		var options = new SchemaRegistrySslOptions
		{
			EnableSslCertificateVerification = false
		};

		// Assert
		options.EnableSslCertificateVerification.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomCertificatePaths()
	{
		// Arrange & Act
		var options = new SchemaRegistrySslOptions
		{
			SslCaLocation = "/certs/ca.crt",
			SslKeystoreLocation = "/certs/client.p12",
			SslKeystorePassword = "secret"
		};

		// Assert. NOTE: storing a value is not the load-bearing property -- that it reaches the client
		// configuration is, and that is asserted in ConfluentSchemaRegistryClientConfigShould.
		options.SslCaLocation.ShouldBe("/certs/ca.crt");
		options.SslKeystoreLocation.ShouldBe("/certs/client.p12");
		options.SslKeystorePassword.ShouldBe("secret");
	}
}
