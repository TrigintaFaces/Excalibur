// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.MongoDB.Cdc;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbCdcRecoveryOptionsShould
{
	[Fact]
	public void HaveDefaultRecoveryStrategy()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
	}

	[Fact]
	public void HaveNullOnPositionResetByDefault()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.OnPositionReset.ShouldBeNull();
	}

	[Fact]
	public void HaveAutoRecreateStreamEnabledByDefault()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.AutoRecreateStreamOnInvalidToken.ShouldBeTrue();
	}

	[Fact]
	public void HaveUseClusterTimeOnResumeFailureEnabledByDefault()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.UseClusterTimeOnResumeFailure.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultMaxRecoveryAttempts()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.MaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRecoveryAttemptDelay()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveAlwaysInvokeCallbackOnResetEnabledByDefault()
	{
		var options = new MongoDbCdcRecoveryOptions();

		options.AlwaysInvokeCallbackOnReset.ShouldBeTrue();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new MongoDbCdcRecoveryOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenInvokeCallbackStrategyWithNoCallback()
	{
		var options = new MongoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = null
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ValidateWithInvokeCallbackAndCallback()
	{
		var options = new MongoDbCdcRecoveryOptions
		{
			RecoveryStrategy = StalePositionRecoveryStrategy.InvokeCallback,
			OnPositionReset = (_, _) => Task.CompletedTask
		};

		Should.NotThrow(() => options.Validate());
	}
}
