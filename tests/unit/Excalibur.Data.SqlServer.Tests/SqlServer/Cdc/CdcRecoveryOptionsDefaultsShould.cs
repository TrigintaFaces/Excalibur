// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcRecoveryOptionsDefaultsShould
{
	[Fact]
	public void HaveFallbackToEarliestAsDefaultRecoveryStrategy()
	{
		CdcRecoveryOptionsDefaults.DefaultRecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
	}

	[Fact]
	public void HaveThreeAsDefaultMaxRecoveryAttempts()
	{
		CdcRecoveryOptionsDefaults.DefaultMaxRecoveryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveOneThousandAsDefaultRecoveryAttemptDelayMs()
	{
		CdcRecoveryOptionsDefaults.DefaultRecoveryAttemptDelayMs.ShouldBe(1000);
	}

	[Fact]
	public void HaveTrueAsDefaultEnableStructuredLogging()
	{
		CdcRecoveryOptionsDefaults.DefaultEnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyArrayAsDefaultCaptureInstances()
	{
		DatabaseConfigDefaults.CdcDefaultCaptureInstances.ShouldBeEmpty();
	}
}
