// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresCdcRecoveryOptionsShould
{
	[Fact]
	public void HaveDefaultRecoveryStrategy()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
	}

	[Fact]
	public void HaveNullOnPositionResetByDefault()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.OnPositionReset.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxRecoveryAttempts()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.MaxRecoveryAttempts.ShouldBe(PostgresCdcRecoveryOptions.DefaultMaxRecoveryAttempts);
		options.MaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRecoveryAttemptDelay()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(PostgresCdcRecoveryOptions.DefaultRecoveryAttemptDelaySeconds));
	}

	[Fact]
	public void HaveStructuredLoggingEnabledByDefault()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void HaveAutoRecreateSlotEnabledByDefault()
	{
		var options = new PostgresCdcRecoveryOptions();

		options.AutoRecreateSlotOnInvalidation.ShouldBeTrue();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new PostgresCdcRecoveryOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenInvokeCallbackStrategyWithNoCallback()
	{
		var options = new PostgresCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ValidateWithInvokeCallbackAndCallback()
	{
		var options = new PostgresCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (_, _) => Task.CompletedTask
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenMaxRecoveryAttemptsIsNegative()
	{
		var options = new PostgresCdcRecoveryOptions
		{
			MaxRecoveryAttempts = -1
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenRecoveryAttemptDelayIsNegative()
	{
		var options = new PostgresCdcRecoveryOptions
		{
			RecoveryAttemptDelay = TimeSpan.FromSeconds(-1)
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void HaveCorrectDefaultConstants()
	{
		PostgresCdcRecoveryOptions.DefaultMaxRecoveryAttempts.ShouldBe(3);
		PostgresCdcRecoveryOptions.DefaultRecoveryAttemptDelaySeconds.ShouldBe(1);
	}
}
