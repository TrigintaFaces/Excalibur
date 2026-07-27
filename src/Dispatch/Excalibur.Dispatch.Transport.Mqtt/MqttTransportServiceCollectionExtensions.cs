// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the MQTT transport.
/// </summary>
public static class MqttTransportServiceCollectionExtensions
{
	/// <summary>
	/// Registers a named MQTT transport: the keyed <see cref="ITransportSender"/>/<see cref="ITransportReceiver"/>,
	/// the connection provider, and validated <see cref="MqttOptions"/>. Keying by <paramref name="name"/> lets
	/// multiple transports coexist — consumers resolve via <c>GetRequiredKeyedService&lt;ITransportSender&gt;(name)</c>
	/// (the framework's multi-transport convention, matching every other transport).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name — the service key used to resolve the sender/receiver.</param>
	/// <param name="configure">Configures the MQTT connection options.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddMqttTransport(
		this IServiceCollection services,
		string name,
		Action<MqttOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MqttOptions>(name)
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MqttOptions>, MqttOptionsValidator>());

		services.TryAddKeyedSingleton<IMqttConnectionProvider>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<MqttOptions>>().Get(name);
			return new MqttConnectionProvider(options);
		});

		services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var provider = sp.GetRequiredKeyedService<IMqttConnectionProvider>(name);
			var options = sp.GetRequiredService<IOptionsMonitor<MqttOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<MqttTransportSender>>();
			return new MqttTransportSender(provider, options, logger);
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var provider = sp.GetRequiredKeyedService<IMqttConnectionProvider>(name);
			var options = sp.GetRequiredService<IOptionsMonitor<MqttOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<MqttTransportReceiver>>();
			return new MqttTransportReceiver(provider, options, logger);
		});

		return services;
	}
}
