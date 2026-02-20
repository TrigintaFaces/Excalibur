// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
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
		options.CertificateFingerprint.ShouldBeNull();
		options.Username.ShouldBeNull();
		options.Password.ShouldBeNull();
		options.ApiKey.ShouldBeNull();
		options.Base64ApiKey.ShouldBeNull();
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.PingTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaximumConnectionsPerNode.ShouldBe(80);
		options.DisableCertificateValidation.ShouldBeFalse();
		options.EnableSniffing.ShouldBeFalse();
		options.SniffingInterval.ShouldBe(TimeSpan.FromHours(1));
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
			CertificateFingerprint = "abc123",
			Username = "elastic",
			Password = "secret",
			ApiKey = "x",
			Base64ApiKey = "x",
			RequestTimeout = TimeSpan.FromMinutes(1),
			PingTimeout = TimeSpan.FromSeconds(10),
			MaximumConnectionsPerNode = 100,
			DisableCertificateValidation = true,
			EnableSniffing = true,
			SniffingInterval = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.Url.ShouldBe(url);
		options.Urls.ShouldBe(urls);
		options.CloudId.ShouldBe("my-cloud-id");
		options.ConnectionPoolType.ShouldBe(ConnectionPoolType.Sniffing);
		options.CertificateFingerprint.ShouldBe("abc123");
		options.Username.ShouldBe("elastic");
		options.Password.ShouldBe("secret");
		options.ApiKey.ShouldBe("x");
		options.Base64ApiKey.ShouldBe("x");
		options.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.PingTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaximumConnectionsPerNode.ShouldBe(100);
		options.DisableCertificateValidation.ShouldBeTrue();
		options.EnableSniffing.ShouldBeTrue();
		options.SniffingInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
