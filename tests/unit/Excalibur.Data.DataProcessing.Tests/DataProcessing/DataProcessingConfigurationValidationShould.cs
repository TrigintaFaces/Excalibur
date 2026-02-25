// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Validation edge case tests for <see cref="DataProcessingConfiguration"/>.
/// Complements the existing DataProcessingConfigurationShould tests.
/// </summary>
[UnitTest]
public sealed class DataProcessingConfigurationValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Throw_WhenDispatcherTimeoutMilliseconds_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingConfiguration { DispatcherTimeoutMilliseconds = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenMaxAttempts_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingConfiguration { MaxAttempts = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenQueueSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingConfiguration { QueueSize = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenProducerBatchSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingConfiguration { ProducerBatchSize = invalidValue });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Throw_WhenConsumerBatchSize_IsZeroOrNegative(int invalidValue)
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingConfiguration { ConsumerBatchSize = invalidValue });
	}

	[Fact]
	public void Throw_WhenTableName_IsNull()
	{
		Should.Throw<ArgumentException>(() =>
			new DataProcessingConfiguration { TableName = null! });
	}

	[Fact]
	public void Throw_WhenTableName_IsWhitespace()
	{
		Should.Throw<ArgumentException>(() =>
			new DataProcessingConfiguration { TableName = "   " });
	}

	[Fact]
	public void Accept_ValidCustomValues()
	{
		// Arrange & Act
		var config = new DataProcessingConfiguration
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
