// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.MultiTransport;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering multi-transport outbox services.
/// </summary>
public static class MultiTransportOutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds multi-transport outbox support, decorating the existing <see cref="IOutboxStore"/>
	/// registration with transport routing capabilities.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure the multi-transport options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configure is null. </exception>
	public static IServiceCollection AddMultiTransportOutbox(
		this IServiceCollection services,
		Action<MultiTransportOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MultiTransportOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register MultiTransportOutboxStore as a decorator over the existing IOutboxStore
		services.TryAddSingleton<Excalibur.Outbox.MultiTransport.IMultiTransportOutboxStore>(sp =>
		{
			var innerStore = sp.GetRequiredService<IOutboxStore>();
			var options = sp.GetRequiredService<Options.IOptions<MultiTransportOutboxOptions>>();
			var logger = sp.GetRequiredService<Logging.ILogger<MultiTransportOutboxStore>>();
			return new MultiTransportOutboxStore(innerStore, options, logger);
		});

		return services;
	}
}
