// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Google;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>Owns a distinct SDK publisher per named bus while preserving explicit client overrides.</summary>
internal static class NamedTopicPublisherRegistration
{
	internal static void Register(IServiceCollection services, string name)
	{
		var state = services.Select(d => d.GetImplementationInstance()).OfType<PublisherRegistrationState>().FirstOrDefault();
		if (state is null)
		{
			state = new PublisherRegistrationState();
			services.AddSingleton(state);
		}
		var previous = FindPublisher(services);
		services.TryAddSingleton<PublisherClient>(sp => CreateBuilder(GetOptions(sp, name)).Build());
		if (previous is null)
		{
			state.FrameworkPublisher = FindPublisher(services);
		}

		services.TryAddKeyedSingleton<TopicPublisherClientAdapter>(name, (sp, _) =>
		{
			var effective = FindPublisher(services);
			var supplied = sp.GetKeyedService<PublisherClient>(name)
				?? (effective is not null && !ReferenceEquals(effective, state.FrameworkPublisher)
					? sp.GetRequiredService<PublisherClient>() : null);
			var client = supplied ?? CreateBuilder(GetOptions(sp, name)).Build();
			// No dependency resolution occurs after acquiring the owned client. The adapter constructor
			// only assigns this non-null SDK client and its ownership flag.
			return new TopicPublisherClientAdapter(client, ownsClient: supplied is null);
		});
		services.TryAddKeyedSingleton<GooglePubSubMessageBus>(name, (sp, _) =>
		{
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = GetOptions(sp, name);
			var logger = sp.GetRequiredService<ILogger<GooglePubSubMessageBus>>();
			return new GooglePubSubMessageBus(sp.GetRequiredKeyedService<TopicPublisherClientAdapter>(name), serializer, options, logger);
		});
		_ = services.AddRemoteMessageBus(name, sp => sp.GetRequiredKeyedService<GooglePubSubMessageBus>(name));
	}

	internal static PublisherClientBuilder CreateBuilder(GooglePubSubOptions options) => new()
	{
		TopicName = new TopicName(options.Connection.ProjectId, options.Connection.TopicId),
		EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
	};

	private static GooglePubSubOptions GetOptions(IServiceProvider provider, string name) =>
		provider.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get(name);

	private static ServiceDescriptor? FindPublisher(IServiceCollection services) =>
		services.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(PublisherClient));

	private sealed class PublisherRegistrationState
	{
		internal ServiceDescriptor? FrameworkPublisher { get; set; }
	}
}
