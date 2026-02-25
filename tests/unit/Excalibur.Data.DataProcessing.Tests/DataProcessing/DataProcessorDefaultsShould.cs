// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessorDefaults"/> constants.
/// </summary>
[UnitTest]
public sealed class DataProcessorDefaultsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaultTableName()
	{
		DataProcessorDefaults.DataProcessorDefaultTableName.ShouldBe("DataProcessor.DataTaskRequests");
	}

	[Fact]
	public void HaveExpectedDefaultDispatcherTimeout()
	{
		DataProcessorDefaults.DataProcessorDefaultDispatcherTimeout.ShouldBe(60000);
	}

	[Fact]
	public void HaveExpectedDefaultMaxAttempts()
	{
		DataProcessorDefaults.DataProcessorDefaultMaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveExpectedDefaultQueueSize()
	{
		DataProcessorDefaults.DataProcessorDefaultQueueSize.ShouldBe(5000);
	}

	[Fact]
	public void HaveExpectedDefaultProducerBatchSize()
	{
		DataProcessorDefaults.DataProcessorDefaultProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveExpectedDefaultConsumerBatchSize()
	{
		DataProcessorDefaults.DataProcessorDefaultConsumerBatchSize.ShouldBe(10);
	}
}
