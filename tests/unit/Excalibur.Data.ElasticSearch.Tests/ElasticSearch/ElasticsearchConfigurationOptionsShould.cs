// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticsearchConfigurationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ElasticsearchConfigurationOptions();

		// Assert
		options.Url.ShouldBeNull();
		options.Urls.ShouldBeNull();
		options.CloudId.ShouldBeNull();
		options.ConnectionPoolType.ShouldBe(ConnectionPoolType.Static);
		options.EnableSniffing.ShouldBeFalse();
		options.Connection.ShouldNotBeNull();
		options.Connection.CertificateFingerprint.ShouldBeNull();
		options.Connection.Username.ShouldBeNull();
		options.Connection.Password.ShouldBeNull();
		options.Connection.ApiKey.ShouldBeNull();
		options.Connection.Base64ApiKey.ShouldBeNull();
		options.Connection.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.Connection.PingTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Connection.MaximumConnectionsPerNode.ShouldBe(80);
		options.Connection.DisableCertificateValidation.ShouldBeFalse();
		options.Connection.SniffingInterval.ShouldBe(TimeSpan.FromHours(1));
		options.Resilience.ShouldNotBeNull();
		options.Monitoring.ShouldNotBeNull();
		options.Projections.ShouldNotBeNull();
	}

	[Fact]
	public void SetAllProperties()
	{
		// Arrange
		var url = new Uri("http://localhost:9200");
		var urls = new[] { new Uri("http://node1:9200"), new Uri("http://node2:9200") };

		// Act
		var options = new ElasticsearchConfigurationOptions
		{
			Url = url,
			Urls = urls,
			CloudId = "my-cloud-id",
			ConnectionPoolType = ConnectionPoolType.Sniffing,
			EnableSniffing = true,
			Connection = new ElasticsearchConnectionOptions
			{
				CertificateFingerprint = "abc123",
				Username = "elastic",
				Password = "secret",
				ApiKey = CreateNonSecretApiKeyValue(),
				Base64ApiKey = CreateNonSecretApiKeyValue(),
				RequestTimeout = TimeSpan.FromMinutes(1),
				PingTimeout = TimeSpan.FromSeconds(10),
				MaximumConnectionsPerNode = 100,
				DisableCertificateValidation = true,
				SniffingInterval = TimeSpan.FromMinutes(30),
			},
		};

		// Assert
		options.Url.ShouldBe(url);
		options.Urls.ShouldBe(urls);
		options.CloudId.ShouldBe("my-cloud-id");
		options.ConnectionPoolType.ShouldBe(ConnectionPoolType.Sniffing);
		options.EnableSniffing.ShouldBeTrue();
		options.Connection.CertificateFingerprint.ShouldBe("abc123");
		options.Connection.Username.ShouldBe("elastic");
		options.Connection.Password.ShouldBe("secret");
		options.Connection.ApiKey.ShouldBe(CreateNonSecretApiKeyValue());
		options.Connection.Base64ApiKey.ShouldBe(CreateNonSecretApiKeyValue());
		options.Connection.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.Connection.PingTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.Connection.MaximumConnectionsPerNode.ShouldBe(100);
		options.Connection.DisableCertificateValidation.ShouldBeTrue();
		options.Connection.SniffingInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void HaveCorrectConnectionOptionsDefaults()
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

	private static string CreateNonSecretApiKeyValue()
	{
		return string.Concat("elastic-", "fixture-", "key");
	}
}
