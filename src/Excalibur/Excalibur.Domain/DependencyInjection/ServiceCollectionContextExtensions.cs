// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Net.Sockets;

using Excalibur.Dispatch.Abstractions;

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
	/// Registers <see cref="ITenantId" /> as a scoped service with a fixed tenant value.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <param name="tenant"> The tenant identifier value. Defaults to <see cref="TenantDefaults.DefaultTenantId"/>. </param>
	/// <returns> The updated service collection. </returns>
	/// <remarks>
	/// When called without arguments, registers <see cref="TenantDefaults.DefaultTenantId"/> ("Default")
	/// as the tenant identifier. For multi-tenant applications that serve multiple tenants from a single
	/// instance, use the <see cref="TryAddTenantId(IServiceCollection, Func{IServiceProvider, string})"/>
	/// overload to resolve the tenant per-request.
	/// Uses TryAdd so existing registrations are preserved.
	/// </remarks>
	public static IServiceCollection TryAddTenantId(this IServiceCollection services, string tenant = TenantDefaults.DefaultTenantId)
	{
		ArgumentNullException.ThrowIfNull(services);
		var resolvedTenant = string.IsNullOrEmpty(tenant) ? TenantDefaults.DefaultTenantId : tenant;
		services.TryAdd(ServiceDescriptor.Scoped<ITenantId>(_ => new TenantId(resolvedTenant)));
		return services;
	}

	/// <summary>
	/// Registers <see cref="ITenantId" /> as a scoped service with per-request tenant resolution.
	/// </summary>
	/// <param name="services"> The service collection to modify. </param>
	/// <param name="tenantResolver"> A factory that resolves the tenant identifier from the service provider.
	/// Called once per scope (per-request in ASP.NET Core). </param>
	/// <returns> The updated service collection. </returns>
	/// <remarks>
	/// <para>
	/// Use this overload for multi-tenant applications that serve multiple tenants from a single instance.
	/// The resolver is called once per scope, allowing per-request tenant resolution from HTTP headers,
	/// claims, or other request-scoped context.
	/// </para>
	/// <para>
	/// Uses TryAdd so existing registrations are preserved. Register this before calling
	/// <c>AddExcaliburA3Services</c> to prevent the default tenant from being used.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.TryAddTenantId(sp =>
	/// {
	///     var httpContext = sp.GetRequiredService&lt;IHttpContextAccessor&gt;().HttpContext;
	///     return httpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()
	///         ?? TenantDefaults.DefaultTenantId;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection TryAddTenantId(this IServiceCollection services, Func<IServiceProvider, string> tenantResolver)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(tenantResolver);
		services.TryAdd(ServiceDescriptor.Scoped<ITenantId>(sp => new TenantId(tenantResolver(sp))));
		return services;
	}

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
	/// <param name="tenant"> The tenant identifier. Defaults to <see cref="TenantDefaults.DefaultTenantId"/>. </param>
	/// <param name="localAddress"> Use the machine IP address when true; otherwise register a scoped address. </param>
	/// <returns> The updated service collection. </returns>
	public static IServiceCollection AddExcaliburContextServices(
		this IServiceCollection services,
		string tenant = TenantDefaults.DefaultTenantId,
		bool localAddress = false)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.TryAddTenantId(tenant);
		_ = services.TryAddCorrelationId();
		_ = services.TryAddETag();
		_ = localAddress
			? services.TryAddLocalClientAddress()
			: services.TryAddClientAddress();

		return services;
	}
}
