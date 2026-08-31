// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;
using Excalibur.Dispatch.Transport.Grpc.DeadLetter;
using Excalibur.Dispatch.Transport.Grpc.Diagnostics;

using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering gRPC transport with the service collection.
/// </summary>
public static class GrpcTransportServiceCollectionExtensions
{
	/// <summary>
	/// Adds the gRPC transport with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The options configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		Action<GrpcTransportOptions> configure)
		=> AddGrpcTransport(services, "default", configure);

	/// <summary>
	/// Adds the gRPC transport using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="GrpcTransportOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		IConfiguration configuration)
		=> AddGrpcTransport(services, "default", configuration);

	/// <summary>
	/// Adds the gRPC transport with the specified name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name used as the keyed service key.</param>
	/// <param name="configure">The options configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		string name,
		Action<GrpcTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// NAMED, so two named gRPC transports in one container no longer write the same instance and
		// let the second silently replace the first. The unnamed registration stays for the single-host
		// path: GrpcTransportSender, GrpcTransportReceiver and GrpcTransportSubscriber take
		// IOptions<GrpcTransportOptions>, which resolves the unnamed instance.
		_ = services.AddOptions<GrpcTransportOptions>(name)
			.Configure(configure)
			.ValidateOnStart();
		_ = services.AddOptions<GrpcTransportOptions>()
			.Configure(configure)
			.ValidateOnStart();

		RegisterGrpcCore(services, name);

		return services;
	}

	/// <summary>
	/// Adds the gRPC transport with the specified name using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name used as the keyed service key.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="GrpcTransportOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "AOT-safe: IConfiguration.Bind() requires reflection -- see Action<T> overload as AOT alternative")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "AOT-safe: IConfiguration.Bind() requires dynamic code -- see Action<T> overload as AOT alternative")]
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		string name,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configuration);

		// Named for multi-transport independence; unnamed for the IOptions<T> consumers. See the
		// Action<T> overload above.
		_ = services.AddOptions<GrpcTransportOptions>(name)
			.Bind(configuration)
			.ValidateOnStart();
		_ = services.AddOptions<GrpcTransportOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		RegisterGrpcCore(services, name);

		return services;
	}

	/// <summary>
	/// Registers the core gRPC transport services shared by all overloads.
	/// </summary>
	private static void RegisterGrpcCore(IServiceCollection services, string name)
	{
		// The channel carries the server address and the message-size and retry limits, so a channel
		// shared between two named transports would send one transport's traffic to the other's server.
		// It is keyed by name; the unnamed registration remains for consumers that inject GrpcChannel
		// directly (the health check, and the single-transport host).
		services.TryAddSingleton(
			sp => CreateChannel(sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value));

		services.TryAddKeyedSingleton(
			name,
			(sp, _) => CreateChannel(NamedOptions(sp, name).Value));

		services.AddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredKeyedService<GrpcChannel>(name);
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSender>>();
			return new GrpcTransportSender(channel, NamedOptions(sp, name), logger);
		});

		services.AddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredKeyedService<GrpcChannel>(name);
			var logger = sp.GetRequiredService<ILogger<GrpcTransportReceiver>>();
			return new GrpcTransportReceiver(channel, NamedOptions(sp, name), logger);
		});

		services.AddKeyedSingleton<ITransportSubscriber>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredKeyedService<GrpcChannel>(name);
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSubscriber>>();
			return new GrpcTransportSubscriber(channel, NamedOptions(sp, name), logger);
		});

		// Register in-memory DLQ manager for gRPC transport (gRPC has no native DLQ)
		services.AddKeyedSingleton<IDeadLetterQueueManager>(name, (sp, _) =>
			new GrpcDeadLetterQueueManager(
				sp.GetRequiredService<ILogger<GrpcDeadLetterQueueManager>>()));

		// Register IValidateOptions for cross-property validation
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GrpcTransportOptions>, GrpcTransportOptionsValidator>());

		// Register health check
		services.TryAddSingleton<GrpcTransportHealthCheck>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHealthCheck, GrpcTransportHealthCheck>());

		// Register transport adapter (bridges gRPC to dispatch pipeline), KEYED by transport name so a
		// second named gRPC transport gets its own adapter instead of a by-type TryAddSingleton
		// silently keeping the first transport's adapter — and therefore its channel — for every
		// name. The adapter MUST resolve the KEYED gRPC sender/channel (registered by 'name' above)
		// explicitly — implicit construction would inject an unkeyed ITransportSender, which is not
		// registered (throws) or cross-wires to another transport.
		services.AddKeyedSingleton<GrpcTransportAdapter>(name, (sp, key) => new GrpcTransportAdapter(
			sp.GetRequiredKeyedService<GrpcChannel>(key),
			sp.GetRequiredKeyedService<ITransportSender>(key),
			sp.GetRequiredService<ILogger<GrpcTransportAdapter>>()));
		// Expose the keyed adapter as ITransportAdapter/ITransportHealthChecker under the same key.
		services.AddKeyedSingleton<ITransportAdapter>(name, (sp, key) => sp.GetRequiredKeyedService<GrpcTransportAdapter>(key));
		services.AddKeyedSingleton<ITransportHealthChecker>(name, (sp, key) => sp.GetRequiredKeyedService<GrpcTransportAdapter>(key));

		// Unkeyed convenience registrations for the single-transport host, mirroring every other
		// transport's unkeyed adapter accessor. TryAdd*, so the first-registered named transport wins
		// — a multi-transport host must resolve the keyed adapter by name instead.
		services.TryAddSingleton(sp => sp.GetRequiredKeyedService<GrpcTransportAdapter>(name));
		services.TryAddSingleton<ITransportAdapter>(static sp => sp.GetRequiredService<GrpcTransportAdapter>());
		services.TryAddSingleton<ITransportHealthChecker>(static sp => sp.GetRequiredService<GrpcTransportAdapter>());
	}

	/// <summary>
	/// Reads this transport's own configuration out of the named options and re-wraps it as
	/// <see cref="IOptions{TOptions}"/> for the transport components, whose public constructors take
	/// that type. Without the re-wrap they would resolve the unnamed instance, which under two named
	/// registrations holds whichever registration ran last.
	/// </summary>
	private static IOptions<GrpcTransportOptions> NamedOptions(IServiceProvider sp, string name)
		=> Microsoft.Extensions.Options.Options.Create(
			sp.GetRequiredService<IOptionsMonitor<GrpcTransportOptions>>().Get(name));

	/// <summary>
	/// Builds the <see cref="GrpcChannel"/> for one transport's resolved options.
	/// </summary>
	private static GrpcChannel CreateChannel(GrpcTransportOptions options)
	{
		// Every channel this package builds — named, unnamed and the health check's — is created here, so
		// the posture has one site and cannot be reached around by resolving a differently-keyed channel.
		RequireSecureAddress(options);

		var channelOptions = new GrpcChannelOptions
		{
			HttpHandler = BuildKeepAliveHandler(options),
		};

		if (options.MaxSendMessageSize.HasValue)
		{
			channelOptions.MaxSendMessageSize = options.MaxSendMessageSize.Value;
		}

		if (options.MaxReceiveMessageSize.HasValue)
		{
			channelOptions.MaxReceiveMessageSize = options.MaxReceiveMessageSize.Value;
		}

		var serviceConfig = BuildServiceConfig(options);
		if (serviceConfig is not null)
		{
			channelOptions.ServiceConfig = serviceConfig;
			channelOptions.MaxRetryAttempts = options.MaxRetryAttempts;
		}

		return GrpcChannel.ForAddress(options.ServerAddress, channelOptions);
	}

	/// <summary>
	/// Refuses a cleartext server address while the secure-by-default posture is in force.
	/// </summary>
	/// <param name="options">The resolved transport options.</param>
	/// <exception cref="TransportSecurityException">
	/// Thrown when <see cref="GrpcTransportOptions.RequireTls"/> is set and
	/// <see cref="GrpcTransportOptions.ServerAddress"/> does not name an <c>https</c> endpoint.
	/// </exception>
	/// <remarks>
	/// An address that does not parse as an absolute URI is refused too. "Cannot tell" is never given the
	/// benefit of the doubt for a security control — <c>GrpcChannel.ForAddress</c> would reject it moments
	/// later anyway, and refusing here keeps one failure shape for one misconfiguration.
	/// </remarks>
	internal static void RequireSecureAddress(GrpcTransportOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.RequireTls)
		{
			return;
		}

		if (Uri.TryCreate(options.ServerAddress, UriKind.Absolute, out var address)
			&& address.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		throw new TransportSecurityException(
			$"Cannot create the gRPC channel: TLS is required but the server address is '{options.ServerAddress}', "
			+ "which is not an https endpoint, so call metadata and message payloads would cross the wire in the "
			+ "clear. Set GrpcTransportOptions.ServerAddress to an https address, or set "
			+ "GrpcTransportOptions.RequireTls to false to accept a cleartext connection.")
		{
			TransportName = "gRPC",
			FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
		};
	}

	/// <summary>
	/// Builds a <see cref="SocketsHttpHandler"/> configured with HTTP/2 keep-alive and connection
	/// pooling settings sourced from <paramref name="options"/>. Keep-alive pings prevent
	/// long-lived subscribe streams from going half-open through idle NAT/load-balancer timeouts.
	/// </summary>
	internal static SocketsHttpHandler BuildKeepAliveHandler(GrpcTransportOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return new SocketsHttpHandler
		{
			KeepAlivePingDelay = TimeSpan.FromSeconds(options.KeepAlivePingDelaySeconds),
			KeepAlivePingTimeout = TimeSpan.FromSeconds(options.KeepAlivePingTimeoutSeconds),
			KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
			PooledConnectionIdleTimeout = TimeSpan.FromSeconds(options.PooledConnectionIdleTimeoutSeconds),
			EnableMultipleHttp2Connections = options.EnableMultipleHttp2Connections,
		};
	}

	/// <summary>
	/// Builds a default gRPC <see cref="ServiceConfig"/> carrying a retry (or hedging) policy that
	/// applies to all methods, or <see langword="null"/> when neither retries nor hedging are enabled.
	/// </summary>
	internal static ServiceConfig? BuildServiceConfig(GrpcTransportOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.EnableRetries && !options.EnableHedging)
		{
			return null;
		}

		var methodConfig = new MethodConfig
		{
			Names = { MethodName.Default },
		};

		if (options.EnableHedging)
		{
			var hedgingPolicy = new HedgingPolicy
			{
				MaxAttempts = options.MaxRetryAttempts,
				HedgingDelay = TimeSpan.FromSeconds(options.RetryInitialBackoffSeconds),
			};

			foreach (var statusCode in options.RetryableStatusCodes)
			{
				hedgingPolicy.NonFatalStatusCodes.Add(statusCode);
			}

			methodConfig.HedgingPolicy = hedgingPolicy;
		}
		else
		{
			var retryPolicy = new RetryPolicy
			{
				MaxAttempts = options.MaxRetryAttempts,
				InitialBackoff = TimeSpan.FromSeconds(options.RetryInitialBackoffSeconds),
				MaxBackoff = TimeSpan.FromSeconds(options.RetryMaxBackoffSeconds),
				BackoffMultiplier = options.RetryBackoffMultiplier,
			};

			foreach (var statusCode in options.RetryableStatusCodes)
			{
				retryPolicy.RetryableStatusCodes.Add(statusCode);
			}

			methodConfig.RetryPolicy = retryPolicy;
		}

		return new ServiceConfig
		{
			MethodConfigs = { methodConfig },
		};
	}
}
