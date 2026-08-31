// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Options.Threading;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PerformanceOptionsShould
{

	// --- MicroBatchOptions ---

	[Fact]
	public void MicroBatchOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MicroBatchOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void MicroBatchOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 500,
			MaxBatchDelay = TimeSpan.FromMilliseconds(500),
		};

		// Assert
		options.MaxBatchSize.ShouldBe(500);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}



	// --- ThreadingOptions ---

	[Fact]
	public void ThreadingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ThreadingOptions();

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(0);
		options.PrefetchBufferSize.ShouldBe(0);
	}

	[Fact]
	public void ThreadingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ThreadingOptions
		{
			DefaultMaxDegreeOfParallelism = 16,
			PrefetchBufferSize = 64,
		};

		// Assert
		options.DefaultMaxDegreeOfParallelism.ShouldBe(16);
		options.PrefetchBufferSize.ShouldBe(64);
	}
}
