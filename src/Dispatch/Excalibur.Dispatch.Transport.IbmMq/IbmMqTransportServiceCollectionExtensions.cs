// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
			// The CloudEvents send path, on the same terms as every other transport that has one. It was
			// absent here for a real reason and that reason is now gone: structured mode is identified by
			// its media type alone, and until the content type could survive this transport the encoder
			// would have produced a body no conformant receiver could recognise.
			return new IbmMqTransportSender(provider, options.QueueName, logger).WithCloudEventEncoding();
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var provider = sp.GetRequiredKeyedService<IIbmMqConnectionProvider>(name);
			var options = sp.GetRequiredService<IOptionsMonitor<IbmMqOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<IbmMqTransportReceiver>>();
			// The IBM MQ spelling, not the house one. The house convention's hyphenated names are
			// UNSETTABLE on this platform -- a property name is validated as a Java identifier, so the
			// queue manager refuses the hyphen and both the sender and a real queue drop the property
			// rather than failing the send. Decoding a spelling nothing can put on the wire made every
			// binary-mode CloudEvent arrive here as ordinary traffic.
			return new IbmMqTransportReceiver(provider, options.QueueName, options.Receive, logger).WithCloudEventDecoding(CloudEventBinding.IbmMq);
		});

		return services;
	}
}
