// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Categories;

using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

[Trait("Category", "Unit")]
public class GooglePubSubDisposalShould
{
	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void DisposeBatchProcessorWithoutThrowing()
	{
		var options = OptionsFactory.Create(new BatchConfiguration());
		var metrics = new BatchMetricsCollector();
		var processor = new ParallelBatchProcessor(
				options,
				static (_, _) => Task.FromResult(new object()),
				NullLogger<ParallelBatchProcessor>.Instance,
				metrics);

		processor.Dispose();
		metrics.Dispose();
	}

	[Fact]
	[UnitTest]
	[Trait("Component", "Transport")]
	[Trait("Pattern", "TRANSPORT")]
	public void DisposeMetricsWithoutThrowing()
	{
		var metrics = new GooglePubSubMetrics();
		metrics.Dispose();
	}
}
