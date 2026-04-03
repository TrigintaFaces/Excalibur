// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Hosting.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension methods for registering all available Dispatch health checks in a single call.
/// </summary>
/// <remarks>
/// <para>
/// This extension conditionally registers health checks based on which services are available
/// in the DI container. Only health checks whose prerequisite services are registered will be added.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// services.AddHealthChecks()
///     .AddDispatchHealthChecks();
///
/// // Or with options to exclude specific checks:
/// services.AddHealthChecks()
///     .AddDispatchHealthChecks(options =&gt;
///     {
///         options.IncludeLeaderElection = false;
///     });
/// </code>
/// </para>
/// </remarks>
public static class DispatchHealthChecksBuilderExtensions
{
	private const string OutboxPublisherTypeName = "Excalibur.Dispatch.Abstractions.IOutboxPublisher";
	private const string InboxStoreTypeName = "Excalibur.Dispatch.Abstractions.IInboxStore";
	private const string SagaMonitoringServiceTypeName = "Excalibur.Saga.Abstractions.ISagaMonitoringService";
	private const string LeaderElectionTypeName = "Excalibur.Dispatch.LeaderElection.ILeaderElection";

	private static readonly (string AssemblyName, string TypeName, string MethodName)[] HealthCheckExtensionTargets =
	[
		("Excalibur.Outbox", "Microsoft.Extensions.DependencyInjection.BackgroundProcessorHealthChecksBuilderExtensions", "AddOutboxHealthCheck"),
		("Excalibur.Outbox", "Microsoft.Extensions.DependencyInjection.BackgroundProcessorHealthChecksBuilderExtensions", "AddInboxHealthCheck"),
		("Excalibur.Saga", "Microsoft.Extensions.DependencyInjection.SagaHealthChecksBuilderExtensions", "AddSagaHealthCheck"),
		("Excalibur.LeaderElection", "Microsoft.Extensions.DependencyInjection.LeaderElectionHealthChecksBuilderExtensions", "AddLeaderElectionHealthCheck"),
	];

	/// <summary>
	/// Adds all available Dispatch health checks (outbox, inbox, saga, leader election)
	/// based on which services are registered in the DI container.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional action to configure which health checks to include.</param>
	/// <returns>The health checks builder for chaining.</returns>
	[RequiresUnreferencedCode("Uses AppDomain.GetAssemblies() and reflection to discover and invoke health check extension methods at runtime.")]
	public static IHealthChecksBuilder AddDispatchHealthChecks(
		this IHealthChecksBuilder builder,
		Action<DispatchHealthCheckOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new DispatchHealthCheckOptions();
		configure?.Invoke(options);

		var services = builder.Services;

		if (options.IncludeOutbox && HasService(services, OutboxPublisherTypeName))
		{
			_ = TryInvokeHealthCheckExtension(builder, "AddOutboxHealthCheck");
		}

		if (options.IncludeInbox && HasService(services, InboxStoreTypeName))
		{
			_ = TryInvokeHealthCheckExtension(builder, "AddInboxHealthCheck");
		}

		if (options.IncludeSaga && HasService(services, SagaMonitoringServiceTypeName))
		{
			_ = TryInvokeHealthCheckExtension(builder, "AddSagaHealthCheck");
		}

		if (options.IncludeLeaderElection && HasService(services, LeaderElectionTypeName))
		{
			_ = TryInvokeHealthCheckExtension(builder, "AddLeaderElectionHealthCheck");
		}

		return builder;
	}

	private static bool HasService(IServiceCollection services, string serviceTypeFullName)
	{
		foreach (var descriptor in services)
		{
			if (string.Equals(descriptor.ServiceType.FullName, serviceTypeFullName, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	[RequiresUnreferencedCode("Uses reflection to discover and invoke health check extension methods.")]
	private static bool TryInvokeHealthCheckExtension(IHealthChecksBuilder builder, string methodName)
	{
		foreach (var target in HealthCheckExtensionTargets)
		{
			if (!string.Equals(target.MethodName, methodName, StringComparison.Ordinal))
			{
				continue;
			}

			var type = ResolveType(target.AssemblyName, target.TypeName);
			if (type is null)
			{
				continue;
			}

			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
				{
					continue;
				}

				var parameters = method.GetParameters();
				if (parameters.Length == 0 || parameters[0].ParameterType != typeof(IHealthChecksBuilder))
				{
					continue;
				}

				var args = new object?[parameters.Length];
				args[0] = builder;

				var allOptional = true;
				for (var i = 1; i < parameters.Length; i++)
				{
					if (!parameters[i].IsOptional)
					{
						allOptional = false;
						break;
					}

					args[i] = parameters[i].DefaultValue;
				}

				if (!allOptional)
				{
					continue;
				}

				_ = method.Invoke(null, args);
				return true;
			}
		}

		return false;
	}

	[RequiresUnreferencedCode("Uses AppDomain.GetAssemblies() and Assembly.GetType() for runtime type resolution.")]
	private static Type? ResolveType(string assemblyName, string typeName)
	{
		foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (!string.Equals(loadedAssembly.GetName().Name, assemblyName, StringComparison.Ordinal))
			{
				continue;
			}

			return loadedAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
		}

		try
		{
			var assembly = Assembly.Load(new AssemblyName(assemblyName));
			return assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
		}
		catch
		{
			return null;
		}
	}
}
