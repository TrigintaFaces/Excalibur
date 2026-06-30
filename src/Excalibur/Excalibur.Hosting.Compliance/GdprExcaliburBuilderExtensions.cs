// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Hosting.Builders;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for GDPR compliance (erasure) configuration.
/// </summary>
/// <remarks>
/// <para>
/// Thin bridge onto <see cref="IExcaliburBuilder"/> forwarding to the existing
/// <c>AddGdprErasure</c> <see cref="IServiceCollection"/> extensions in
/// <c>Excalibur.Compliance</c>. Closes -A6 composition-root gap per
/// (Compliance package placement) — the physical package rename /
/// migration to <c>Excalibur.Compliance</c> is deferred to per
/// §Open-Questions §2, so this bridge ships in the separate
/// <c>Excalibur.Hosting.Compliance</c> package to avoid pulling heavy
/// compliance transitive dependencies (MongoDB.Driver, Npgsql, QuestPDF) into
/// every consumer of <c>Excalibur.Hosting</c>.
/// </para>
/// </remarks>
public static class GdprExcaliburBuilderExtensions
{
	/// <summary>
	/// Adds GDPR erasure services within the Excalibur composition root.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configureOptions">Optional configuration action for <see cref="ErasureOptions"/>.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .AddDispatch(...)
	///     .AddGdprErasure(opts => opts.RetentionDays = 30));
	/// </code>
	/// </example>
	public static IExcaliburBuilder AddGdprErasure(
		this IExcaliburBuilder builder,
		Action<ErasureOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddGdprErasure(configureOptions);
		return builder;
	}

	/// <summary>
	/// Adds GDPR erasure services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ErasureOptions"/>.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IExcaliburBuilder AddGdprErasure(
		this IExcaliburBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddGdprErasure(configuration);
		return builder;
	}
}
