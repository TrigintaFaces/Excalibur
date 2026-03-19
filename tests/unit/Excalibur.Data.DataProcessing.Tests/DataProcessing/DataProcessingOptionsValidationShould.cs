// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Validation edge case tests for <see cref="DataProcessingOptions"/>.
/// Complements the existing DataProcessingOptionsShould tests.
/// </summary>
[UnitTest]
public sealed class DataProcessingOptionsValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Throw_WhenDispatcherTimeoutMilliseconds_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { DispatcherTimeoutMilliseconds = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenMaxAttempts_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { MaxAttempts = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenQueueSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { QueueSize = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenProducerBatchSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { ProducerBatchSize = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenConsumerBatchSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { ConsumerBatchSize = invalidValue });
	}

	[Fact]
	public void Throw_WhenTableName_IsNull()
	{
		Should.Throw<ArgumentException>(() =>
			new DataProcessingOptions { TableName = null! });
	}

	[Fact]
	public void Throw_WhenTableName_IsWhitespace()
	{
		Should.Throw<ArgumentException>(() =>
			new DataProcessingOptions { TableName = "   " });
	}

	[Fact]
	public void Accept_ValidCustomValues()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			TableName = "Custom.Table",
			DispatcherTimeoutMilliseconds = 1,
			MaxAttempts = 1,
			QueueSize = 1,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
		};

		// Assert — minimum valid values accepted
		config.TableName.ShouldBe("Custom.Table");
		config.DispatcherTimeoutMilliseconds.ShouldBe(1);
		config.MaxAttempts.ShouldBe(1);
		config.QueueSize.ShouldBe(1);
		config.ProducerBatchSize.ShouldBe(1);
		config.ConsumerBatchSize.ShouldBe(1);
	}
}
