// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Sentinel;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Sentinel;

/// <summary>
/// Unit tests for <see cref="SentinelExporterOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class SentinelExporterOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultLogType()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.LogType.ShouldBe("DispatchAudit");
	}

	[Fact]
	public void HaveNullDefaultAzureResourceId()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.AzureResourceId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTimeGeneratedField()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.TimeGeneratedField.ShouldBe("timestamp");
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(500);
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
	public void HaveDefaultTimeout()
	{
		// Arrange
		var options = CreateValidOptions();

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion Default Value Tests

	#region Property Setting Tests

	[Fact]
	public void AllowWorkspaceIdToBeSet()
	{
		// Arrange
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "my-workspace-guid",
			SharedKey = "test-key"
		};

		// Assert
		options.WorkspaceId.ShouldBe("my-workspace-guid");
	}

	[Fact]
	public void AllowSharedKeyToBeSet()
	{
		// Arrange
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace",
			SharedKey = "my-secret-shared-key-base64"
		};

		// Assert
		options.SharedKey.ShouldBe("my-secret-shared-key-base64");
	}

	[Fact]
	public void AllowLogTypeToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.LogType = "CustomAudit";

		// Assert
		options.LogType.ShouldBe("CustomAudit");
	}

	[Fact]
	public void AllowAzureResourceIdToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.AzureResourceId = "/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/containerApps/myapp";

		// Assert
		options.AzureResourceId.ShouldBe("/subscriptions/123/resourceGroups/rg/providers/Microsoft.App/containerApps/myapp");
	}

	[Fact]
	public void AllowTimeGeneratedFieldToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.TimeGeneratedField = "event_time";

		// Assert
		options.TimeGeneratedField.ShouldBe("event_time");
	}

	[Fact]
	public void AllowTimeGeneratedFieldToBeNull()
	{
		// Arrange
		var options = CreateValidOptions();
		options.TimeGeneratedField = null;

		// Assert
		options.TimeGeneratedField.ShouldBeNull();
	}

	[Fact]
	public void AllowMaxBatchSizeToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.MaxBatchSize = 1000;

		// Assert
		options.MaxBatchSize.ShouldBe(1000);
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
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var options = CreateValidOptions();
		options.Timeout = TimeSpan.FromSeconds(60);

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	#endregion Property Setting Tests

	#region Validation Attribute Tests

	[Fact]
	public void HaveRequiredAttributeOnWorkspaceId()
	{
		// Arrange
		var propertyInfo = typeof(SentinelExporterOptions).GetProperty(nameof(SentinelExporterOptions.WorkspaceId));

		// Act
		var hasRequired = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any();

		// Assert
		hasRequired.ShouldBeTrue();
	}

	[Fact]
	public void HaveRequiredAttributeOnSharedKey()
	{
		// Arrange
		var propertyInfo = typeof(SentinelExporterOptions).GetProperty(nameof(SentinelExporterOptions.SharedKey));

		// Act
		var hasRequired = propertyInfo.GetCustomAttributes(
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any();

		// Assert
		hasRequired.ShouldBeTrue();
	}

	#endregion Validation Attribute Tests

	#region Log Type Naming Convention Tests

	[Theory]
	[InlineData("DispatchAudit")]
	[InlineData("SecurityEvents")]
	[InlineData("CustomLog_Type123")]
	public void AcceptValidLogTypeValues(string logType)
	{
		// Arrange & Act
		var options = CreateValidOptions();
		options.LogType = logType;

		// Assert
		options.LogType.ShouldBe(logType);
	}

	#endregion Log Type Naming Convention Tests

	private static SentinelExporterOptions CreateValidOptions()
	{
		return new SentinelExporterOptions
		{
			WorkspaceId = "test-workspace-guid",
			SharedKey = "dGVzdC1zaGFyZWQta2V5LWJhc2U2NA==" // base64 encoded
		};
	}
}
