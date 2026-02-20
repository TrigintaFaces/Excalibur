// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MultiBusSample;

/// <summary>
/// Extension methods for configuring Kafka message bus.
/// </summary>
public static class KafkaServiceExtensions
{
	/// <summary>
	/// Adds Kafka message bus to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <returns> The service collection. </returns>
	public static IServiceCollection AddKafkaMessageBus(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<KafkaOptions>(configuration.GetSection("Kafka"));

		_ = services.AddSingleton(_ =>
		{
			var config = new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] };
			return new ProducerBuilder<string, byte[]>(config).Build();
		});

		_ = services.AddRemoteMessageBus(
			"kafka",
			sp => new KafkaMessageBus(
				sp.GetRequiredService<IProducer<string, byte[]>>(),
				sp.GetRequiredService<IPayloadSerializer>(),
				sp.GetRequiredService<IOptions<KafkaOptions>>().Value,
				sp.GetRequiredService<ILogger<KafkaMessageBus>>()));

		return services;
	}
}
