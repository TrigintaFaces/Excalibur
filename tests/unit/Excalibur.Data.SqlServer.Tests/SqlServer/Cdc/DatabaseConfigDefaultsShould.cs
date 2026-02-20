// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class DatabaseConfigDefaultsShould
{
	[Fact]
	public void DefaultBatchTimeIntervalTo5000()
	{
		DatabaseConfigDefaults.CdcDefaultBatchTimeInterval.ShouldBe(5000);
	}

	[Fact]
	public void DefaultQueueSizeTo1000()
	{
		DatabaseConfigDefaults.CdcDefaultQueueSize.ShouldBe(1000);
	}

	[Fact]
	public void DefaultProducerBatchSizeTo100()
	{
		DatabaseConfigDefaults.CdcDefaultProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void DefaultConsumerBatchSizeTo10()
	{
		DatabaseConfigDefaults.CdcDefaultConsumerBatchSize.ShouldBe(10);
	}

	[Fact]
	public void DefaultStopOnMissingTableHandlerToTrue()
	{
		DatabaseConfigDefaults.CdcDefaultStopOnMissingTableHandler.ShouldBeTrue();
	}

	[Fact]
	public void DefaultCaptureInstancesToEmptyArray()
	{
		DatabaseConfigDefaults.CdcDefaultCaptureInstances.ShouldBeEmpty();
	}
}
