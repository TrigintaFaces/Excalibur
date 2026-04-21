// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for leader election configuration.
/// </summary>
public static class LeaderElectionExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures leader election for the Excalibur host using the builder pattern.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">
	/// Action to configure leader election via the builder, including provider selection
	/// (e.g., <c>UseSqlServer</c>) and optional features (e.g., <c>WithHealthChecks</c>).
	/// </param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(exc =&gt; exc
	///     .AddLeaderElection(le =&gt; le
	///         .UseSqlServer(sql =&gt; sql
	///             .ConnectionString("Server=...;Database=...")
	///             .LockResource("MyApp.Leader"))
	///         .WithHealthChecks()));
	/// </code>
	/// </example>
	public static IExcaliburBuilder AddLeaderElection(
		this IExcaliburBuilder builder,
		Action<ILeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddExcaliburLeaderElection(configure);

		return builder;
	}
}
