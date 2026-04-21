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
		options.SslKeyLocation.ShouldBeNull();
		options.SslCertificateLocation.ShouldBeNull();
		options.SslKeyPassword.ShouldBeNull();
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
			SslKeyLocation = "/certs/client.key",
			SslCertificateLocation = "/certs/client.crt",
			SslKeyPassword = "secret"
		};

		// Assert
		options.SslCaLocation.ShouldBe("/certs/ca.crt");
		options.SslKeyLocation.ShouldBe("/certs/client.key");
		options.SslCertificateLocation.ShouldBe("/certs/client.crt");
		options.SslKeyPassword.ShouldBe("secret");
	}
}
