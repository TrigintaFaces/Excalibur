// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CoreOptionsDepthShould
{
	// --- CompressionOptions ---

	[Fact]
	public void CompressionOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CompressionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.CompressionType.ShouldBe(CompressionType.Gzip);
		options.CompressionLevel.ShouldBe(6);
		options.MinimumSizeThreshold.ShouldBe(1024);
	}

	[Fact]
	public void CompressionOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CompressionOptions
		{
			Enabled = true,
			CompressionType = CompressionType.Brotli,
			CompressionLevel = 9,
			MinimumSizeThreshold = 512,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.CompressionType.ShouldBe(CompressionType.Brotli);
		options.CompressionLevel.ShouldBe(9);
		options.MinimumSizeThreshold.ShouldBe(512);
	}

	// --- CompressionType enum ---

	[Fact]
	public void CompressionType_HasExpectedValues()
	{
		// Assert
		((int)CompressionType.None).ShouldBe(0);
		((int)CompressionType.Gzip).ShouldBe(1);
		((int)CompressionType.Deflate).ShouldBe(2);
		((int)CompressionType.Lz4).ShouldBe(3);
		((int)CompressionType.Brotli).ShouldBe(4);
	}

	[Fact]
	public void CompressionType_HasExpectedCount()
	{
		// Assert
		Enum.GetValues<CompressionType>().Length.ShouldBe(5);
	}

	// --- HealthCheckOptions ---

	[Fact]
	public void HealthCheckOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new HealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HealthCheckOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new HealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	// --- InMemoryBusOptions ---

	[Fact]
	public void InMemoryBusOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InMemoryBusOptions();

		// Assert
		options.MaxQueueLength.ShouldBe(1000);
		options.PreserveOrder.ShouldBeTrue();
		options.ProcessingDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void InMemoryBusOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			MaxQueueLength = 500,
			PreserveOrder = false,
			ProcessingDelay = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.MaxQueueLength.ShouldBe(500);
		options.PreserveOrder.ShouldBeFalse();
		options.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}


	// --- MessageBusHealthCheckOptions ---

	[Fact]
	public void MessageBusHealthCheckOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
		options.FailureThreshold.ShouldBe(3);
	}

	[Fact]
	public void MessageBusHealthCheckOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MessageBusHealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromMinutes(1),
			FailureThreshold = 5,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
		options.FailureThreshold.ShouldBe(5);
	}


	// --- TracingOptions ---

	[Fact]
	public void TracingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new TracingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(1.0);
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void TracingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new TracingOptions
		{
			Enabled = true,
			SamplingRatio = 0.5,
			IncludeSensitiveData = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.SamplingRatio.ShouldBe(0.5);
		options.IncludeSensitiveData.ShouldBeTrue();
	}
}
