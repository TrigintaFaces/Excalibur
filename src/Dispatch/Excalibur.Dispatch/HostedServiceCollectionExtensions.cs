// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Delivery;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Dispatch messaging hosted services.
/// </summary>
/// <remarks>
/// For outbox and inbox hosted services, use Excalibur.Outbox's DI extensions.
/// </remarks>
public static class HostedServiceCollectionExtensions
{
	// Note: AddOutboxHostedService and AddInboxHostedService have been moved to Excalibur.Outbox
	// Use Excalibur.Outbox DI extensions for those services

	/// <summary>
	/// Registers the <see cref="ScheduledMessageService" /> as a hosted service.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	public static IServiceCollection AddDispatchSchedulingHostedService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddHostedService<ScheduledMessageService>();

		return services;
	}
}
