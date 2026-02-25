// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Support;

internal static class BenchmarkDispatchServiceCollectionExtensions
{
	public static IServiceCollection AddBenchmarkDispatch(
		this IServiceCollection services,
		Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddDispatch(builder =>
		{
			_ = builder.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.Features.EnableCacheMiddleware = true;
				options.Features.EnableCorrelation = false;
				options.Inbox.Enabled = false;
				options.Consumer.Dedupe.Enabled = true;
				options.Consumer.AckAfterHandle = true;
				options.Outbox.BatchSize = 100;
				options.Outbox.PublishIntervalMs = 1000;
				options.Outbox.UseInMemoryStorage = true;
				options.CrossCutting.Observability.EnableContextFlow = false;
			});

			configure?.Invoke(builder);
		});
	}
}
