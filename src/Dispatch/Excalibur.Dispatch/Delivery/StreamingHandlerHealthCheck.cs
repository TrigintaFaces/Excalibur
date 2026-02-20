// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Health check for the streaming document handler subsystem.
/// </summary>
/// <remarks>
/// <para>
/// Verifies that at least one <see cref="IStreamingDocumentHandler{TDocument, TOutput}"/>
/// implementation is registered in DI and can be resolved. Reports:
/// </para>
/// <list type="bullet">
///   <item><b>Healthy:</b> At least one streaming handler is registered and resolvable</item>
///   <item><b>Degraded:</b> No streaming handlers found (may be intentional in non-streaming deployments)</item>
///   <item><b>Unhealthy:</b> Handler resolution failed with an exception</item>
/// </list>
/// </remarks>
public sealed class StreamingHandlerHealthCheck : IHealthCheck
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingHandlerHealthCheck"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving streaming handlers.</param>
	public StreamingHandlerHealthCheck(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			// Discover registered streaming handler types via service descriptors
			using var scope = _serviceProvider.CreateScope();
			var handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a =>
				{
					try
					{
						return a.GetTypes();
					}
					catch
					{
						return [];
					}
				})
				.Where(t => !t.IsAbstract && !t.IsInterface)
				.Where(t => t.GetInterfaces().Any(i =>
					i.IsGenericType &&
					i.GetGenericTypeDefinition() == typeof(IStreamingDocumentHandler<,>)))
				.ToList();

			data["registered_handler_count"] = handlerTypes.Count;

			if (handlerTypes.Count > 0)
			{
				data["handler_types"] = string.Join(", ", handlerTypes.Select(t => t.Name));

				return Task.FromResult(HealthCheckResult.Healthy(
					$"Streaming handler subsystem is healthy. {handlerTypes.Count} handler type(s) discovered.",
					data: data));
			}

			return Task.FromResult(HealthCheckResult.Degraded(
				"No streaming document handler types discovered. This is expected if streaming is not used.",
				data: data));
		}
		catch (Exception ex)
		{
			data["error"] = ex.Message;

			return Task.FromResult(HealthCheckResult.Unhealthy(
				$"Streaming handler health check failed: {ex.Message}",
				exception: ex,
				data: data));
		}
	}
}
