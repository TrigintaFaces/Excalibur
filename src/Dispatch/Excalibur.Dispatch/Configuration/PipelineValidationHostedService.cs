// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Validates the dispatch middleware pipeline at application startup.
/// Detects empty pipelines, missing handler middleware, and logs warnings
/// for duplicate middleware or questionable ordering.
/// </summary>
internal sealed partial class PipelineValidationHostedService(
	IServiceProvider serviceProvider,
	ILogger<PipelineValidationHostedService> logger) : IHostedService
{
	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		var middlewares = serviceProvider.GetServices<IDispatchMiddleware>().ToList();

		if (middlewares.Count == 0)
		{
			throw new InvalidOperationException(
				"No dispatch middleware registered. The pipeline is empty and cannot process messages. " +
				"Register middleware via AddDispatch(builder => builder.UseMiddleware<T>()) or enable pipeline synthesis.");
		}

		// if any pipeline profile DECLARES the authorization stage but no
		// AuthorizationMiddleware instance is resolvable in the pipeline, authorization is silently
		// absent for messages routed through that profile — a fail-open security gap. Fail closed at
		// startup. (The Default profile declares no authorization, so a non-authorizing application is
		// unaffected; only a profile that explicitly expects authorization is guarded.) This composes
		// with the registered-but-feature-disabled guard in DispatchBuilder.
		// fire only when the DEFAULT (selected) profile declares authorization but no
		// AuthorizationMiddleware is resolvable — i.e. the consumer INTENDS auth (they selected an
		// auth-declaring profile as the default, e.g. SetDefaultProfile("strict")) yet forgot to wire it.
		// Keying on the default profile — not "any registered profile" — is essential: the framework
		// ALWAYS seeds a Strict profile that declares authorization, so checking any-registered would
		// over-fire on the out-of-the-box AddDefaultDispatchPipelines() path (default = "default",
		// declares no auth) and break every non-authorizing app at startup. Composes with the
		// registered-but-feature-disabled guard in DispatchBuilder.
		var profileRegistry = serviceProvider.GetService<IPipelineProfileRegistry>();
		var defaultProfileName = profileRegistry?.GetDefaultProfileName();
		var defaultProfile = defaultProfileName is null ? null : profileRegistry!.GetProfile(defaultProfileName);
		// Read the SAME declaration the pipeline builder consults. This guard and UseProfile must never
		// consult different collections: if one could see an authorization entry the other could not,
		// the check would pass against a list the built pipeline never used.
		if (defaultProfile is not null
			&& defaultProfile.MiddlewareEntries.Any(static e => e.MiddlewareType == typeof(AuthorizationMiddleware))
			&& !middlewares.Exists(static m => m.Unwrap() is AuthorizationMiddleware))
		{
			throw new InvalidOperationException(
				"A pipeline profile declares the authorization stage (AuthorizationMiddleware), but no " +
				"AuthorizationMiddleware is registered or resolvable in the dispatch pipeline. Messages " +
				"routed through that profile would silently bypass authorization. Register the authorization " +
				"middleware (e.g. AddDispatch(builder => builder.UseAuthorization())) or remove the " +
				"authorization stage from the profile.");
		}

		// Check for duplicate middleware types in same stage
		var grouped = middlewares
			.Where(static m => m.Stage.HasValue)
			.GroupBy(static m => (m.Stage, m.UnwrappedType()));

		foreach (var group in grouped)
		{
			if (group.Count() > 1)
			{
				LogDuplicateMiddleware(logger, group.Key.Item2.Name, group.Key.Stage?.ToString() ?? "Unknown");
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(2200, LogLevel.Warning,
		"Duplicate middleware '{MiddlewareName}' registered in stage '{Stage}'. This may cause unexpected double-execution.")]
	private static partial void LogDuplicateMiddleware(ILogger logger, string middlewareName, string stage);
}
