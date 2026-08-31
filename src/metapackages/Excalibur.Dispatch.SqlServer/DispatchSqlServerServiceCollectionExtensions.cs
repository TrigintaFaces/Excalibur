// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Configuration;
using Excalibur.Outbox.SqlServer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with SQL Server event sourcing and outbox
/// into a single registration call.
/// </summary>
public static class DispatchSqlServerServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with SQL Server event sourcing and a SQL Server outbox store,
	/// both bound to the specified connection string.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The outbox registered here <em>stages</em> messages durably; it does not drain them. No background
	/// delivery service is started, so adding this call does not change the runtime profile of a host that
	/// did not ask for one. To deliver staged messages, opt in explicitly — either
	/// <c>services.AddOutboxHostedService()</c>, or by re-declaring the outbox with
	/// <c>services.AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.UseSqlServer(...).EnableBackgroundProcessing()))</c>.
	/// </para>
	/// <para>
	/// The outbox declared here composes with a later, more specific declaration: calling
	/// <c>AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.UseSqlServer(...)))</c> afterwards overrides the schema,
	/// table, processing and connection settings chosen here, so a consumer who needs to tune the outbox
	/// does not have to abandon this one-line entry point to do it.
	/// </para>
	/// <para>
	/// Not usable under Native AOT: the outbox this registers serializes message payloads reflectively
	/// and there is no ahead-of-time-safe alternative entry point. Under trimming it is usable, but you
	/// must supply <c>JsonSerializerOptions</c> with a source-generated <c>TypeInfoResolver</c> covering
	/// your message types.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureDispatch">Optional dispatch builder configuration. Supplying it takes over handler registration: when it is omitted, handlers are discovered by scanning the entry assembly; when it is supplied, only the handlers it names are registered.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode(
		"The outbox registered here serializes message payloads reflectively. Supply JsonSerializerOptions with a source-generated TypeInfoResolver for your message types to satisfy this under trimming.")]
	[RequiresDynamicCode(
		"The outbox registered here serializes message payloads reflectively, which requires dynamic code generation. There is no ahead-of-time-safe alternative entry point for this stack.")]
	public static IServiceCollection AddDispatchWithSqlServer(
		this IServiceCollection services,
		string connectionString,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = services.AddDispatch(configureDispatch ?? (static d => d.AddHandlersFromEntryAssembly()));
		_ = services.AddSqlServerEventSourcing(options => options.ConnectionString = connectionString);

		// Routed through the IExcaliburBuilder bridge because the flat AddExcaliburOutbox aggregator is
		// internal; this is the same public seam a consumer would use to declare an outbox themselves,
		// which is what makes the two declarations compose rather than conflict.
		_ = services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
			outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))));

		return services;
	}
}
