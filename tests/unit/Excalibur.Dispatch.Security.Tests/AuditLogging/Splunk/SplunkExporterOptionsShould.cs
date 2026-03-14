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
		options.Batch.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultRequestTimeout()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Batch.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Batch.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryBaseDelay()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Batch.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultEnableCompressionEnabled()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Connection.EnableCompression.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultValidateCertificateEnabled()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Connection.ValidateCertificate.ShouldBeTrue();
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
			Connection =
			{
				HecEndpoint = endpoint,
				HecToken = "test-token"
			}
		};

		// Assert
		options.Connection.HecEndpoint.ShouldBe(endpoint);
	}

	[Fact]
	public void AllowHecTokenToBeSet()
	{
		// Arrange
		var options = new SplunkExporterOptions
		{
			Connection =
			{
				HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
				HecToken = "my-hec-token-123"
			}
		};

		// Assert
		options.Connection.HecToken.ShouldBe("my-hec-token-123");
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
		options.Batch.MaxBatchSize = 200;

		// Assert
		options.Batch.MaxBatchSize.ShouldBe(200);
	}

	[Fact]
	public void AllowRequestTimeoutToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Batch.RequestTimeout = TimeSpan.FromSeconds(60);

		// Assert
		options.Batch.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowMaxRetryAttemptsToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Batch.MaxRetryAttempts = 5;

		// Assert
		options.Batch.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowRetryBaseDelayToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Batch.RetryBaseDelay = TimeSpan.FromSeconds(2);

		// Assert
		options.Batch.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void AllowEnableCompressionToBeDisabled()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Connection.EnableCompression = false;

		// Assert
		options.Connection.EnableCompression.ShouldBeFalse();
	}

	[Fact]
	public void AllowValidateCertificateToBeDisabled()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Connection.ValidateCertificate = false;

		// Assert
		options.Connection.ValidateCertificate.ShouldBeFalse();
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
		var propertyInfo = typeof(SplunkConnectionOptions).GetProperty(nameof(SplunkConnectionOptions.HecEndpoint));

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
		var propertyInfo = typeof(SplunkConnectionOptions).GetProperty(nameof(SplunkConnectionOptions.HecToken));

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
		var propertyInfo = typeof(SplunkBatchOptions).GetProperty(nameof(SplunkBatchOptions.MaxBatchSize));

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
		var propertyInfo = typeof(SplunkBatchOptions).GetProperty(nameof(SplunkBatchOptions.MaxRetryAttempts));

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
			Connection =
			{
				HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector"),
				HecToken = "test-token"
			}
		};
	}
}
