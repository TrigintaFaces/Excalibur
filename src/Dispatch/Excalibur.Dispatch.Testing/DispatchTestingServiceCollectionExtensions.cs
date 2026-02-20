// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Tracking;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Dispatch testing services.
/// </summary>
public static class DispatchTestingServiceCollectionExtensions
{
	/// <summary>
	/// Registers in-memory transport implementations and test tracking services
	/// for integration testing of Dispatch pipelines.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="destination">The destination name for the in-memory sender. Defaults to <c>"test-destination"</c>.</param>
	/// <param name="source">The source name for the in-memory receiver and subscriber. Defaults to <c>"test-source"</c>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchTesting(
		this IServiceCollection services,
		string destination = "test-destination",
		string source = "test-source")
	{
		ArgumentNullException.ThrowIfNull(services);

		// CA2000: Objects are transferred to DI container which manages their lifetime (IAsyncDisposable)
#pragma warning disable CA2000
		var sender = new InMemoryTransportSender(destination);
		var receiver = new InMemoryTransportReceiver(source);
		var subscriber = new InMemoryTransportSubscriber(source);
#pragma warning restore CA2000

		services.AddSingleton(sender);
		services.AddSingleton<ITransportSender>(sp => sp.GetRequiredService<InMemoryTransportSender>());

		services.AddSingleton(receiver);
		services.AddSingleton<ITransportReceiver>(sp => sp.GetRequiredService<InMemoryTransportReceiver>());

		services.AddSingleton(subscriber);
		services.AddSingleton<ITransportSubscriber>(sp => sp.GetRequiredService<InMemoryTransportSubscriber>());

		services.AddSingleton<IDispatchedMessageLog, DispatchedMessageLog>();

		return services;
	}
}
