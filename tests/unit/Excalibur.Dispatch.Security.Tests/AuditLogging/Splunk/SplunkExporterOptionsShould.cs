// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Splunk;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Splunk;

/// <summary>
/// Unit tests for <see cref="SplunkExporterOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class SplunkExporterOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullDefaultIndex()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Index.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultSourceType()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.SourceType.ShouldBe("audit:dispatch");
	}

	[Fact]
	public void HaveNullDefaultSource()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Source.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDefaultHost()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Host.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultRequestTimeout()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryBaseDelay()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultEnableCompressionEnabled()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultValidateCertificateEnabled()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.ValidateCertificate.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultUseAckDisabled()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.UseAck.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullDefaultChannel()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Channel.ShouldBeNull();
	}

	#endregion Default Value Tests

	#region Property Setting Tests

	[Fact]
	public void AllowHecEndpointToBeSet()
	{
		// Arrange
		var endpoint = new Uri("https://splunk.example.com:8088/services/collector");
		var options = new SplunkExporterOptions
		{
			HecEndpoint = endpoint,
			HecToken = "test-token"
		};

		// Assert
		options.HecEndpoint.ShouldBe(endpoint);
	}

	[Fact]
	public void AllowHecTokenToBeSet()
	{
		// Arrange
		var options = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "my-hec-token-123"
		};

		// Assert
		options.HecToken.ShouldBe("my-hec-token-123");
	}

	[Fact]
	public void AllowIndexToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Index = "audit_logs";

		// Assert
		options.Index.ShouldBe("audit_logs");
	}

	[Fact]
	public void AllowSourceTypeToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.SourceType = "custom:audit";

		// Assert
		options.SourceType.ShouldBe("custom:audit");
	}

	[Fact]
	public void AllowSourceToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Source = "my-application";

		// Assert
		options.Source.ShouldBe("my-application");
	}

	[Fact]
	public void AllowHostToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Host = "server-01";

		// Assert
		options.Host.ShouldBe("server-01");
	}

	[Fact]
	public void AllowMaxBatchSizeToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.MaxBatchSize = 200;

		// Assert
		options.MaxBatchSize.ShouldBe(200);
	}

	[Fact]
	public void AllowRequestTimeoutToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.RequestTimeout = TimeSpan.FromSeconds(60);

		// Assert
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowMaxRetryAttemptsToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowRetryBaseDelayToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.RetryBaseDelay = TimeSpan.FromSeconds(2);

		// Assert
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void AllowEnableCompressionToBeDisabled()
	{
		// Arrange
		var options = CreateValidOptions();
		options.EnableCompression = false;

		// Assert
		options.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void AllowValidateCertificateToBeDisabled()
	{
		// Arrange
		var options = CreateValidOptions();
		options.ValidateCertificate = false;

		// Assert
		options.ValidateCertificate.ShouldBeFalse();
	}

	[Fact]
	public void AllowUseAckToBeEnabled()
	{
		// Arrange
		var options = CreateValidOptions();
		options.UseAck = true;

		// Assert
		options.UseAck.ShouldBeTrue();
	}

	[Fact]
	public void AllowChannelToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Channel = "my-channel-guid";

		// Assert
		options.Channel.ShouldBe("my-channel-guid");
	}

	#endregion Property Setting Tests

	#region Validation Attribute Tests

	[Fact]
	public void HaveRequiredAttributeOnHecEndpoint()
	{
		// Arrange
		var propertyInfo = typeof(SplunkExporterOptions).GetProperty(nameof(SplunkExporterOptions.HecEndpoint));

		// Act
		var hasRequired = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any();

		// Assert
		hasRequired.ShouldBeTrue();
	}

	[Fact]
	public void HaveRequiredAttributeOnHecToken()
	{
		// Arrange
		var propertyInfo = typeof(SplunkExporterOptions).GetProperty(nameof(SplunkExporterOptions.HecToken));

		// Act
		var hasRequired = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any();

		// Assert
		hasRequired.ShouldBeTrue();
	}

	[Fact]
	public void HaveRangeAttributeOnMaxBatchSize()
	{
		// Arrange
		var propertyInfo = typeof(SplunkExporterOptions).GetProperty(nameof(SplunkExporterOptions.MaxBatchSize));

		// Act
		var hasRange = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false).Any();

		// Assert
		hasRange.ShouldBeTrue();
	}

	[Fact]
	public void HaveRangeAttributeOnMaxRetryAttempts()
	{
		// Arrange
		var propertyInfo = typeof(SplunkExporterOptions).GetProperty(nameof(SplunkExporterOptions.MaxRetryAttempts));

		// Act
		var hasRange = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false).Any();

		// Assert
		hasRange.ShouldBeTrue();
	}

	#endregion Validation Attribute Tests

	private static SplunkExporterOptions CreateValidOptions()
	{
		return new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
			HecToken = "test-token"
		};
	}
}
