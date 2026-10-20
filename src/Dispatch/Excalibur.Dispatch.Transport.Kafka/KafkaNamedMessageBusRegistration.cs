// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>Composes named publishing without borrowing another transport's framework-created producer.</summary>
internal static class KafkaNamedMessageBusRegistration
{
	internal static void Register(IServiceCollection services, string name, IReadOnlyDictionary<Type, string> topicMappings)
	{
		var topics = new Dictionary<Type, string>(topicMappings);
		var state = services.Select(d => d.GetImplementationInstance()).OfType<ProducerRegistrationState>().FirstOrDefault();
		if (state is null)
		{
			state = new ProducerRegistrationState();
			services.AddSingleton(state);
		}

		var previous = services.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(IProducer<string, byte[]>));
		services.TryAddSingleton<IProducer<string, byte[]>>(sp => BuildProducer(sp, name));
		if (previous is null)
		{
			state.FrameworkProducer = services.Last(d => !d.IsKeyedService && d.ServiceType == typeof(IProducer<string, byte[]>));
		}

		services.TryAddKeyedSingleton<KafkaMessageBus>(name, (sp, _) =>
		{
			var effectiveProducer = services.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(IProducer<string, byte[]>));
			var hasConsumerProducer = effectiveProducer is not null && !ReferenceEquals(effectiveProducer, state.FrameworkProducer);
			var supplied = sp.GetKeyedService<IProducer<string, byte[]>>(name)
				?? (hasConsumerProducer ? sp.GetRequiredService<IProducer<string, byte[]>>() : null);
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = Microsoft.Extensions.Options.Options.Create(sp.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get(name));
			var logger = sp.GetRequiredService<ILogger<KafkaMessageBus>>();
			var encoder = sp.GetService<ICloudEventEncoder<Message<string, string>>>();
			var cloudEvents = sp.GetRequiredService<IOptionsMonitor<KafkaCloudEventOptions>>().Get(name);
			var bridge = sp.GetService<IEnvelopeCloudEventBridge>();
			var producer = supplied ?? BuildProducer(sp, name);
			try
			{
				return new KafkaMessageBus(producer, serializer, options, logger, encoder, cloudEvents, bridge,
					ownsProducer: supplied is null, topicMappings: topics);
			}
			catch
			{
				if (supplied is null)
				{
					producer.Dispose();
				}
				throw;
			}
		});
		_ = services.AddRemoteMessageBus(name, sp => sp.GetRequiredKeyedService<KafkaMessageBus>(name));
	}

	private static IProducer<string, byte[]> BuildProducer(IServiceProvider provider, string name)
	{
		var options = provider.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get(name);
		var cloudEvents = provider.GetRequiredService<IOptionsMonitor<KafkaCloudEventOptions>>().Get(name);
		return new ProducerBuilder<string, byte[]>(KafkaProducerConfigBuilder.Build(options, cloudEvents)).Build();
	}

	private sealed class ProducerRegistrationState
	{
		internal ServiceDescriptor? FrameworkProducer { get; set; }
	}
}
