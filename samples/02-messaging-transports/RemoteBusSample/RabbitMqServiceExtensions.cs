// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace RemoteBusSample;

/// <summary>
/// Extension methods for configuring RabbitMQ message bus.
/// </summary>
public static class RabbitMqServiceExtensions
{
	/// <summary>
	/// Adds RabbitMQ message bus to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <returns> The service collection. </returns>
	public static IServiceCollection AddRabbitMqMessageBus(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

		_ = services.AddSingleton(sp =>
		{
			var connectionString = configuration["RabbitMq:ConnectionString"];
			if (string.IsNullOrEmpty(connectionString))
			{
				throw new InvalidOperationException("RabbitMq:ConnectionString configuration is required");
			}

			var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
			return factory.CreateConnectionAsync().GetAwaiter().GetResult();
		});

		_ = services.AddSingleton(sp =>
		{
			var connection = sp.GetRequiredService<IConnection>();
			return connection.CreateChannelAsync().GetAwaiter().GetResult();
		});

		_ = services.AddRemoteMessageBus(
			"rabbit",
			sp => new RabbitMqMessageBus(
				sp.GetRequiredService<IChannel>(),
				sp.GetRequiredService<IPayloadSerializer>(),
				sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value,
				sp.GetRequiredService<ILogger<RabbitMqMessageBus>>()));

		return services;
	}
}
