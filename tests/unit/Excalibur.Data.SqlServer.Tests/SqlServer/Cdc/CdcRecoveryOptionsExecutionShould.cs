// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests for <see cref="CdcRecoveryOptions"/> execution scenarios:
/// strategy invocation, callback execution, validation on invoke,
/// max attempts exhaustion, and delay configuration edge cases.
/// Complements CdcRecoveryOptionsShould (which covers defaults + simple validation).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcRecoveryOptionsExecutionShould : UnitTestBase
{
	[Fact]
	public void FallbackToEarliest_WithCustomAttempts_ConfiguresCorrectly()
	{
		// Arrange & Act
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest,
			MaxRecoveryAttempts = 5,
			RecoveryAttemptDelay = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.MaxRecoveryAttempts.ShouldBe(5);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(10));
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void FallbackToLatest_PassesValidation()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest,
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Throw_Strategy_PassesValidation()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.Throw,
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void InvokeCallback_WithCallback_PassesValidation()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (_, _) => Task.CompletedTask,
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void InvokeCallback_WithoutCallback_FailsValidation()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null,
		};

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("OnPositionReset");
	}

	[Fact]
	public async Task OnPositionReset_CallbackReceivesCorrectParameters()
	{
		// Arrange
		CdcPositionResetEventArgs? capturedArgs = null;
		CancellationToken capturedToken = default;

		var options = new CdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (args, ct) =>
			{
				capturedArgs = args;
				capturedToken = ct;
				return Task.CompletedTask;
			},
		};

		using var cts = new CancellationTokenSource();
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			DatabaseName = "my-database",
			ProviderType = "SqlServer",
			CaptureInstance = "dbo_orders",
		};

		// Act
		await options.OnPositionReset!(eventArgs, cts.Token).ConfigureAwait(false);

		// Assert
		capturedArgs.ShouldNotBeNull();
		capturedArgs.ProcessorId.ShouldBe("test-processor");
		capturedArgs.DatabaseName.ShouldBe("my-database");
		capturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public void MaxRecoveryAttempts_CanBeSetToZero_ForNoRetries()
	{
		var options = new CdcRecoveryOptions
		{
			MaxRecoveryAttempts = 0,
		};

		options.MaxRecoveryAttempts.ShouldBe(0);
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void NegativeMaxRecoveryAttempts_FailsValidation()
	{
		var options = new CdcRecoveryOptions
		{
			MaxRecoveryAttempts = -1,
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void NegativeRecoveryAttemptDelay_FailsValidation()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1),
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void RecoveryAttemptDelay_CanBeSetToZero_ForImmediateRetries()
	{
		var options = new CdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.Zero,
		};

		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.Zero);
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void EnableStructuredLogging_DefaultsToTrue()
	{
		var options = new CdcRecoveryOptions();

		options.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void EnableStructuredLogging_CanBeDisabled()
	{
		var options = new CdcRecoveryOptions
		{
			EnableStructuredLogging = false,
		};

		options.EnableStructuredLogging.ShouldBeFalse();
		Should.NotThrow(() => options.Validate());
	}
}
