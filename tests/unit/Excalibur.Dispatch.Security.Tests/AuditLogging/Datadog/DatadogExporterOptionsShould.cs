// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Datadog;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Datadog;

/// <summary>
/// Unit tests for <see cref="DatadogExporterOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class DatadogExporterOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultSite()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Site.ShouldBe("datadoghq.com");
	}

	[Fact]
	public void HaveDefaultService()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Service.ShouldBe("dispatch-audit");
	}

	[Fact]
	public void HaveDefaultSource()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Source.ShouldBe("dispatch");
	}

	[Fact]
	public void HaveNullDefaultHostname()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Hostname.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDefaultTags()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Tags.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryBaseDelay()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultTimeout()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultUseCompressionEnabled()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.UseCompression.ShouldBeTrue();
	}

	#endregion Default Value Tests

	#region Property Setting Tests

	[Fact]
	public void AllowApiKeyToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions { ApiKey = "my-api-key-123" };

		// Assert
		options.ApiKey.ShouldBe("my-api-key-123");
	}

	[Fact]
	public void AllowSiteToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Site = "us3.datadoghq.com"
		};

		// Assert
		options.Site.ShouldBe("us3.datadoghq.com");
	}

	[Fact]
	public void AllowServiceToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Service = "my-custom-service"
		};

		// Assert
		options.Service.ShouldBe("my-custom-service");
	}

	[Fact]
	public void AllowSourceToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Source = "custom-source"
		};

		// Assert
		options.Source.ShouldBe("custom-source");
	}

	[Fact]
	public void AllowHostnameToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Hostname = "my-server-01"
		};

		// Assert
		options.Hostname.ShouldBe("my-server-01");
	}

	[Fact]
	public void AllowTagsToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Tags = "env:prod,team:security"
		};

		// Assert
		options.Tags.ShouldBe("env:prod,team:security");
	}

	[Fact]
	public void AllowMaxBatchSizeToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			MaxBatchSize = 100
		};

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void AllowMaxRetryAttemptsToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			MaxRetryAttempts = 5
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowRetryBaseDelayToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			RetryBaseDelay = TimeSpan.FromSeconds(2)
		};

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Timeout = TimeSpan.FromSeconds(60)
		};

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowUseCompressionToBeDisabled()
	{
		// Arrange
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			UseCompression = false
		};

		// Assert
		options.UseCompression.ShouldBeFalse();
	}

	#endregion Property Setting Tests

	#region Validation Attribute Tests

	[Fact]
	public void HaveRequiredAttributeOnApiKey()
	{
		// Arrange
		var propertyInfo = typeof(DatadogExporterOptions).GetProperty(nameof(DatadogExporterOptions.ApiKey));

		// Act
		var hasRequired = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any();

		// Assert
		hasRequired.ShouldBeTrue();
	}

	#endregion Validation Attribute Tests

	#region Site Values Documentation Test

	[Theory]
	[InlineData("datadoghq.com")] // US1
	[InlineData("us3.datadoghq.com")] // US3
	[InlineData("us5.datadoghq.com")] // US5
	[InlineData("datadoghq.eu")] // EU
	[InlineData("ap1.datadoghq.com")] // AP1
	public void AcceptCommonSiteValues(string site)
	{
		// Arrange & Act
		var options = new DatadogExporterOptions
		{
			ApiKey = "test-key",
			Site = site
		};

		// Assert
		options.Site.ShouldBe(site);
	}

	#endregion Site Values Documentation Test
}
