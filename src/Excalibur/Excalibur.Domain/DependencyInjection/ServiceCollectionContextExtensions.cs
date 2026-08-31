// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Net.Sockets;

using Excalibur.Dispatch;

using Excalibur.Domain;
using Excalibur.Domain.Concurrency;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering common context value services.
/// </summary>
public static class ServiceCollectionContextExtensions
{
	/// <summary>
	/// Registers <see cref="ICorrelationId" /> as a scoped service.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection TryAddCorrelationId(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAdd(ServiceDescriptor.Scoped<ICorrelationId, CorrelationId>());
		return services;
	}

	/// <summary>
	/// Registers <see cref="IETag" /> as a scoped service.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection TryAddETag(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAdd(ServiceDescriptor.Scoped<IETag, ETag>());
		return services;
	}

	/// <summary>
	/// Registers <see cref="IClientAddress" /> as a scoped service.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection TryAddClientAddress(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAdd(ServiceDescriptor.Scoped<IClientAddress>(static _ => new ClientAddress()));
		return services;
	}

	/// <summary>
	/// Registers a singleton <see cref="IClientAddress" /> using the machine's primary IP address.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection TryAddLocalClientAddress(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAdd(ServiceDescriptor.Singleton<IClientAddress>(static _ =>
		{
			try
			{
				var ip = Dns.GetHostAddresses(Dns.GetHostName())[0].ToString();
				return new ClientAddress(ip);
			}
			catch (Exception ex) when (ex is SocketException or ArgumentException or InvalidOperationException)
			{
				return new ClientAddress("127.0.0.1");
			}
		}));

		return services;
	}

	/// <summary>
	/// Registers tenant, correlation, ETag and client address services using the Excalibur defaults.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <param name="tenant"> The default tenant identifier applied when no ambient tenant resolves.
	/// When <see langword="null" />, <see cref="TenantDefaults.DefaultTenantId"/> is resolved at run time.
	/// It is deliberately not a default parameter value: a default value is baked into the caller's
	/// assembly at compile time, so a consumer would keep passing the value that was current when they
	/// built, and this value decides which rows an operation can see. </param>
	/// <param name="localAddress"> Use the machine IP address when true; otherwise register a scoped address. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection AddExcaliburContextServices(
		this IServiceCollection services,
		string? tenant = null,
		bool localAddress = false)
	{
		ArgumentNullException.ThrowIfNull(services);

		var resolvedTenant = tenant ?? TenantDefaults.DefaultTenantId;

		_ = services.Configure<TenantContextOptions>(o => o.DefaultTenantId = resolvedTenant);
		_ = services.TryAddCorrelationId();
		_ = services.TryAddETag();
		_ = localAddress
			? services.TryAddLocalClientAddress()
			: services.TryAddClientAddress();

		return services;
	}
}
