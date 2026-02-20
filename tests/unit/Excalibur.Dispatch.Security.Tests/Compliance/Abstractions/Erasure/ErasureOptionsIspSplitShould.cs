// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

/// <summary>
/// Tests for ErasureOptions ISP split (S560.49) -- verifies sub-option binding,
/// nested initializer syntax, backward-compatible shims, and sealed sub-options.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureOptionsIspSplitShould
{
	#region Sub-Options Initialization

	[Fact]
	public void Retention_SubOptions_IsInitializedByDefault()
	{
		var options = new ErasureOptions();

		options.Retention.ShouldNotBeNull();
	}

	[Fact]
	public void Execution_SubOptions_IsInitializedByDefault()
	{
		var options = new ErasureOptions();

		options.Execution.ShouldNotBeNull();
	}

	[Fact]
	public void Retention_SubOptions_HasCorrectDefaults()
	{
		var options = new ErasureOptions();

		options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 7));
		options.Retention.SigningKeyId.ShouldBeNull();
	}

	[Fact]
	public void Execution_SubOptions_HasCorrectDefaults()
	{
		var options = new ErasureOptions();

		options.Execution.BatchSize.ShouldBe(100);
		options.Execution.MaxRetryAttempts.ShouldBe(3);
		options.Execution.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ErasureRetentionOptions_IsSealed()
	{
		typeof(ErasureRetentionOptions).IsSealed.ShouldBeTrue(
			"Sub-option classes should be sealed for immutability");
	}

	[Fact]
	public void ErasureExecutionOptions_IsSealed()
	{
		typeof(ErasureExecutionOptions).IsSealed.ShouldBeTrue(
			"Sub-option classes should be sealed for immutability");
	}

	#endregion

	#region Nested Initializer Syntax

	[Fact]
	public void NestedInitializer_SetsRetentionSubOptions()
	{
		var options = new ErasureOptions
		{
			Retention =
			{
				CertificateRetentionPeriod = TimeSpan.FromDays(365 * 10),
				SigningKeyId = "key-2026"
			}
		};

		options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 10));
		options.Retention.SigningKeyId.ShouldBe("key-2026");
	}

	[Fact]
	public void NestedInitializer_SetsExecutionSubOptions()
	{
		var options = new ErasureOptions
		{
			Execution =
			{
				BatchSize = 200,
				MaxRetryAttempts = 5,
				RetryDelay = TimeSpan.FromMinutes(1)
			}
		};

		options.Execution.BatchSize.ShouldBe(200);
		options.Execution.MaxRetryAttempts.ShouldBe(5);
		options.Execution.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void NestedInitializer_WithRootAndBothSubOptions_WorksTogether()
	{
		var options = new ErasureOptions
		{
			DefaultGracePeriod = TimeSpan.FromHours(24),
			EnableAutoDiscovery = false,
			Retention =
			{
				CertificateRetentionPeriod = TimeSpan.FromDays(365 * 5),
				SigningKeyId = "prod-key"
			},
			Execution =
			{
				BatchSize = 50,
				MaxRetryAttempts = 10
			}
		};

		options.DefaultGracePeriod.ShouldBe(TimeSpan.FromHours(24));
		options.EnableAutoDiscovery.ShouldBeFalse();
		options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 5));
		options.Retention.SigningKeyId.ShouldBe("prod-key");
		options.Execution.BatchSize.ShouldBe(50);
		options.Execution.MaxRetryAttempts.ShouldBe(10);
	}

	#endregion

	#region Backward-Compatible Shims -- Retention

	[Fact]
	public void Shim_CertificateRetentionPeriod_DelegatesToRetention()
	{
		var options = new ErasureOptions();

		// Set via shim
		options.CertificateRetentionPeriod = TimeSpan.FromDays(365 * 10);

		// Read from sub-option
		options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 10));

		// Set via sub-option
		options.Retention.CertificateRetentionPeriod = TimeSpan.FromDays(365 * 3);

		// Read from shim
		options.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 3));
	}

	[Fact]
	public void Shim_SigningKeyId_DelegatesToRetention()
	{
		var options = new ErasureOptions();

		// Set via shim
		options.SigningKeyId = "shim-key";

		// Read from sub-option
		options.Retention.SigningKeyId.ShouldBe("shim-key");

		// Set via sub-option
		options.Retention.SigningKeyId = "direct-key";

		// Read from shim
		options.SigningKeyId.ShouldBe("direct-key");
	}

	#endregion

	#region Backward-Compatible Shims -- Execution

	[Fact]
	public void Shim_BatchSize_DelegatesToExecution()
	{
		var options = new ErasureOptions();

		// Set via shim
		options.BatchSize = 200;

		// Read from sub-option
		options.Execution.BatchSize.ShouldBe(200);

		// Set via sub-option
		options.Execution.BatchSize = 500;

		// Read from shim
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void Shim_MaxRetryAttempts_DelegatesToExecution()
	{
		var options = new ErasureOptions();

		// Set via shim
		options.MaxRetryAttempts = 7;

		// Read from sub-option
		options.Execution.MaxRetryAttempts.ShouldBe(7);

		// Set via sub-option
		options.Execution.MaxRetryAttempts = 10;

		// Read from shim
		options.MaxRetryAttempts.ShouldBe(10);
	}

	[Fact]
	public void Shim_RetryDelay_DelegatesToExecution()
	{
		var options = new ErasureOptions();

		// Set via shim
		options.RetryDelay = TimeSpan.FromMinutes(2);

		// Read from sub-option
		options.Execution.RetryDelay.ShouldBe(TimeSpan.FromMinutes(2));

		// Set via sub-option
		options.Execution.RetryDelay = TimeSpan.FromMinutes(5);

		// Read from shim
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Shim_DefaultValues_MatchSubOptionDefaults()
	{
		var options = new ErasureOptions();

		// Retention shims match Retention sub-option defaults
		options.CertificateRetentionPeriod.ShouldBe(options.Retention.CertificateRetentionPeriod);
		options.SigningKeyId.ShouldBe(options.Retention.SigningKeyId);

		// Execution shims match Execution sub-option defaults
		options.BatchSize.ShouldBe(options.Execution.BatchSize);
		options.MaxRetryAttempts.ShouldBe(options.Execution.MaxRetryAttempts);
		options.RetryDelay.ShouldBe(options.Execution.RetryDelay);
	}

	#endregion

	#region ISP Gate Compliance

	[Fact]
	public void Retention_SubOptions_PropertyCount_ShouldBeWithinGate()
	{
		// ISP gate: sub-option class should have <= 10 properties
		var retentionType = typeof(ErasureRetentionOptions);
		var props = retentionType.GetProperties();

		props.Length.ShouldBeLessThanOrEqualTo(10,
			"ErasureRetentionOptions should have <= 10 properties per ISP gate");
	}

	[Fact]
	public void Execution_SubOptions_PropertyCount_ShouldBeWithinGate()
	{
		// ISP gate: sub-option class should have <= 10 properties
		var executionType = typeof(ErasureExecutionOptions);
		var props = executionType.GetProperties();

		props.Length.ShouldBeLessThanOrEqualTo(10,
			"ErasureExecutionOptions should have <= 10 properties per ISP gate");
	}

	#endregion

	#region Validate Uses Sub-Options

	[Fact]
	public void Validate_ChecksExecution_BatchSize()
	{
		// The Validate() method reads Execution.BatchSize, not a root property
		var options = new ErasureOptions();
		options.Execution.BatchSize = 0;

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ChecksExecution_MaxRetryAttempts()
	{
		// The Validate() method reads Execution.MaxRetryAttempts, not a root property
		var options = new ErasureOptions();
		options.Execution.MaxRetryAttempts = -1;

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_PassesWithValidSubOptions()
	{
		var options = new ErasureOptions
		{
			Retention =
			{
				CertificateRetentionPeriod = TimeSpan.FromDays(365 * 5),
				SigningKeyId = "test-key"
			},
			Execution =
			{
				BatchSize = 50,
				MaxRetryAttempts = 5,
				RetryDelay = TimeSpan.FromMinutes(1)
			}
		};

		Should.NotThrow(() => options.Validate());
	}

	#endregion
}
