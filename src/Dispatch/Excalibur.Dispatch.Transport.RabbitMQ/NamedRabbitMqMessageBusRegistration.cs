// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>Composes named publication with independent default SDK resources and explicit overrides.</summary>
internal static class NamedRabbitMqMessageBusRegistration
{
	internal static void Register(IServiceCollection services, RabbitMQTransportOptions transport)
	{
		var state = services.Select(d => d.GetImplementationInstance()).OfType<RegistrationState>().FirstOrDefault();
		if (state is null)
		{
			state = new RegistrationState();
			services.AddSingleton(state);
		}
		var name = transport.Name;
		RegisterLegacy<IConnectionFactory>(services, state, sp => RabbitMQTransportServiceCollectionExtensions.CreateConnectionFactory(sp, transport));
		RegisterLegacy<IConnection>(services, state, sp => RabbitMQTransportServiceCollectionExtensions.ExecuteSync(
			() => sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync()));
		RegisterLegacy<IChannel>(services, state, sp => RabbitMQTransportServiceCollectionExtensions.ExecuteSync(
			() => sp.GetRequiredService<IConnection>().CreateChannelAsync(CreateChannelOptions(sp, name))));
		services.TryAddKeyedSingleton<NamedRabbitMqChannel>(name, (sp, _) => CreateChannel(sp, services, state, transport));
		var routes = new RabbitMqPublishRoutes(transport.Topology);
		services.TryAddKeyedSingleton<RabbitMqMessageBus>(name, (sp, _) => CreateMessageBus(sp, transport, routes));
		_ = services.AddRemoteMessageBus(name, sp => sp.GetRequiredKeyedService<RabbitMqMessageBus>(name));
	}

	private static RabbitMqMessageBus CreateMessageBus(IServiceProvider provider, RabbitMQTransportOptions transport, RabbitMqPublishRoutes routes)
	{
		var name = transport.Name;
		var options = provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get(name);
		var cloudEvents = provider.GetRequiredService<IOptionsMonitor<RabbitMqCloudEventOptions>>().Get(name);
		var serializer = provider.GetRequiredService<IPayloadSerializer>();
		var logger = provider.GetRequiredService<ILogger<RabbitMqMessageBus>>();
		var encoder = provider.GetService<ICloudEventEncoder<(IBasicProperties properties, ReadOnlyMemory<byte> body)>>();
		var bridge = provider.GetService<IEnvelopeCloudEventBridge>();
		var topology = new RabbitMqTopologyInitializer(options, cloudEvents, provider.GetService<ILogger<RabbitMqTopologyInitializer>>(),
			transport.Topology, transport.EnableDeadLetter ? transport.DeadLetter : null);
		var channel = provider.GetRequiredKeyedService<NamedRabbitMqChannel>(name).Channel;
		return new RabbitMqMessageBus(channel, serializer, Microsoft.Extensions.Options.Options.Create(options), logger,
			bridge, encoder, cloudEvents, topology, routes);
	}

	private static NamedRabbitMqChannel CreateChannel(IServiceProvider provider, IServiceCollection services,
		RegistrationState state, RabbitMQTransportOptions transport)
	{
		var suppliedChannel = GetOverride<IChannel>(provider, services, state, transport.Name);
		if (suppliedChannel is not null)
		{
			return new NamedRabbitMqChannel(suppliedChannel, null, ownsChannel: false, ownsConnection: false);
		}
		var createOptions = CreateChannelOptions(provider, transport.Name);
		var suppliedConnection = GetOverride<IConnection>(provider, services, state, transport.Name);
		var connection = suppliedConnection ?? RabbitMQTransportServiceCollectionExtensions.ExecuteSync(() =>
			(GetOverride<IConnectionFactory>(provider, services, state, transport.Name)
				?? RabbitMQTransportServiceCollectionExtensions.CreateConnectionFactory(provider, transport)).CreateConnectionAsync());
		try
		{
			var channel = RabbitMQTransportServiceCollectionExtensions.ExecuteSync(() => connection.CreateChannelAsync(createOptions));
			return new NamedRabbitMqChannel(channel, connection, ownsChannel: true, ownsConnection: suppliedConnection is null);
		}
		catch (Exception creation)
		{
			if (suppliedConnection is null)
			{
				try
				{
					_ = RabbitMQTransportServiceCollectionExtensions.ExecuteSync(async () =>
					{
						await connection.DisposeAsync().ConfigureAwait(false);
						return true;
					});
				}
				catch (Exception cleanup)
				{
					throw new AggregateException("RabbitMQ channel creation and connection cleanup failed.", creation, cleanup);
				}
			}
			throw;
		}
	}

	private static CreateChannelOptions CreateChannelOptions(IServiceProvider provider, string name)
	{
		var options = provider.GetRequiredService<IOptionsMonitor<RabbitMqCloudEventOptions>>().Get(name);
		var confirms = options.Exchange.EnablePublisherConfirms || options.Publisher.EnableConfirms;
		return new CreateChannelOptions(confirms, confirms, outstandingPublisherConfirmationsRateLimiter: null, consumerDispatchConcurrency: null);
	}

	private static T? GetOverride<T>(IServiceProvider provider, IServiceCollection services, RegistrationState state, string name) where T : class
	{
		var effective = Find<T>(services);
		return provider.GetKeyedService<T>(name) ?? (effective is not null &&
			(!state.Framework.TryGetValue(typeof(T), out var framework) || !ReferenceEquals(effective, framework))
			? provider.GetService<T>() : null);
	}

	private static void RegisterLegacy<T>(IServiceCollection services, RegistrationState state, Func<IServiceProvider, T> factory) where T : class
	{
		var previous = Find<T>(services);
		services.TryAddSingleton(factory);
		if (previous is null)
		{
			state.Framework[typeof(T)] = Find<T>(services)!;
		}
	}

	private static ServiceDescriptor? Find<T>(IServiceCollection services) =>
		services.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(T));

	private sealed class RegistrationState
	{
		internal Dictionary<Type, ServiceDescriptor> Framework { get; } = [];
	}
}
