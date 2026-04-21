// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
public sealed class DatabaseOptionsDefaultsShould
{
	[Fact]
	public void DefaultBatchTimeIntervalTo5000()
	{
		DatabaseOptionsDefaults.CdcDefaultBatchTimeInterval.ShouldBe(5000);
	}

	[Fact]
	public void DefaultQueueSizeTo1000()
	{
		DatabaseOptionsDefaults.CdcDefaultQueueSize.ShouldBe(1000);
	}

	[Fact]
	public void DefaultProducerBatchSizeTo100()
	{
		DatabaseOptionsDefaults.CdcDefaultProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void DefaultConsumerBatchSizeTo10()
	{
		DatabaseOptionsDefaults.CdcDefaultConsumerBatchSize.ShouldBe(10);
	}

	[Fact]
	public void DefaultStopOnMissingTableHandlerToTrue()
	{
		DatabaseOptionsDefaults.CdcDefaultStopOnMissingTableHandler.ShouldBeTrue();
	}

	[Fact]
	public void DefaultCaptureInstancesToEmptyArray()
	{
		DatabaseOptionsDefaults.CdcDefaultCaptureInstances.ShouldBeEmpty();
	}
}
