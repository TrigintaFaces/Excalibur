// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

/// <summary>
/// Unit tests for <see cref="ElasticsearchConnectionOptions"/>.
/// Verifies defaults and property assignment for connection-level settings.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ElasticsearchConnectionOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new ElasticsearchConnectionOptions();

		// Assert
		options.CertificateFingerprint.ShouldBeNull();
		options.Username.ShouldBeNull();
		options.Password.ShouldBeNull();
		options.ApiKey.ShouldBeNull();
		options.Base64ApiKey.ShouldBeNull();
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.PingTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaximumConnectionsPerNode.ShouldBe(80);
		options.DisableCertificateValidation.ShouldBeFalse();
		options.SniffingInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowCustomAuthenticationSettings()
	{
		// Arrange & Act
		var options = new ElasticsearchConnectionOptions
		{
			Username = "elastic",
			Password = "changeme",
			CertificateFingerprint = "abc123"
		};

		// Assert
		options.Username.ShouldBe("elastic");
		options.Password.ShouldBe("changeme");
		options.CertificateFingerprint.ShouldBe("abc123");
	}

	[Fact]
	public void AllowApiKeyAuthentication()
	{
		// Arrange & Act
		var options = new ElasticsearchConnectionOptions
		{
			ApiKey = "my-api-key",
			Base64ApiKey = "bXktYXBpLWtleQ=="
		};

		// Assert
		options.ApiKey.ShouldBe("my-api-key");
		options.Base64ApiKey.ShouldBe("bXktYXBpLWtleQ==");
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new ElasticsearchConnectionOptions
		{
			RequestTimeout = TimeSpan.FromMinutes(2),
			PingTimeout = TimeSpan.FromSeconds(10)
		};

		// Assert
		options.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.PingTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AllowCustomPoolAndSslSettings()
	{
		// Arrange & Act
		var options = new ElasticsearchConnectionOptions
		{
			MaximumConnectionsPerNode = 200,
			DisableCertificateValidation = true,
			SniffingInterval = TimeSpan.FromMinutes(30)
		};

		// Assert
		options.MaximumConnectionsPerNode.ShouldBe(200);
		options.DisableCertificateValidation.ShouldBeTrue();
		options.SniffingInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
