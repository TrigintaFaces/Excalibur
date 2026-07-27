// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the IBM MQ transport.
/// </summary>
public static class IbmMqTransportServiceCollectionExtensions
{
	/// <summary>
	/// Registers a named IBM MQ transport: the keyed <see cref="ITransportSender"/>/<see cref="ITransportReceiver"/>,
	/// the connection provider, and validated <see cref="IbmMqOptions"/>. Keying by <paramref name="name"/> lets
	/// multiple transports coexist — consumers resolve via <c>GetRequiredKeyedService&lt;ITransportSender&gt;(name)</c>
	/// (the framework's multi-transport convention, matching every other transport).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name — the service key used to resolve the sender/receiver.</param>
	/// <param name="configure">Configures the IBM MQ connection options.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddIbmMqTransport(
		this IServiceCollection services,
		string name,
		Action<IbmMqOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<IbmMqOptions>(name)
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<IbmMqOptions>, IbmMqOptionsValidator>());

		services.TryAddKeyedSingleton<IIbmMqConnectionProvider>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<IbmMqOptions>>().Get(name);
			return new IbmMqConnectionProvider(options);
		});

		services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var provider = sp.GetRequiredKeyedService<IIbmMqConnectionProvider>(name);
			var options = sp.GetRequiredService<IOptionsMonitor<IbmMqOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<IbmMqTransportSender>>();
			return new IbmMqTransportSender(provider, options.QueueName, logger);
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var provider = sp.GetRequiredKeyedService<IIbmMqConnectionProvider>(name);
			var options = sp.GetRequiredService<IOptionsMonitor<IbmMqOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<IbmMqTransportReceiver>>();
			return new IbmMqTransportReceiver(provider, options.QueueName, options.Receive, logger);
		});

		return services;
	}
}
