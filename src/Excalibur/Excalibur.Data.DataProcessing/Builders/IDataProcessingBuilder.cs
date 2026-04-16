// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing.Processing;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Fluent builder interface for configuring data processing services.
/// </summary>
/// <remarks>
/// <para>
/// This builder is <b>database-agnostic</b> — it uses <see cref="IDbConnection"/>
/// rather than a specific provider's connection type. Use the appropriate
/// <see cref="IDbConnection"/> implementation for your database.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddDataProcessing(dp =&gt;
/// {
///     dp.ConnectionFactory(() =&gt; new SqlConnection(connectionString))
///       .BindConfiguration("DataProcessing")
///       .AddProcessor&lt;OrderProcessor&gt;()
///       .AddRecordHandler&lt;OrderHandler, OrderRecord&gt;()
///       .EnableBackgroundProcessing();
/// });
/// </code>
/// </para>
/// </remarks>
public interface IDataProcessingBuilder
{
	/// <summary>
	/// Sets a simple factory function that creates database connections.
	/// </summary>
	/// <param name="connectionFactory">A factory that creates <see cref="IDbConnection"/> instances.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	IDataProcessingBuilder ConnectionFactory(Func<IDbConnection> connectionFactory);

	/// <summary>
	/// Sets a DI-aware factory function that creates database connections.
	/// Use for resolving connection configuration from the service provider.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning a
	/// <c>Func&lt;IDbConnection&gt;</c> that creates connections on demand.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	IDataProcessingBuilder ConnectionFactory(Func<IServiceProvider, Func<IDbConnection>> connectionFactory);

	/// <summary>
	/// Binds data processing options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "DataProcessing").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	IDataProcessingBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Registers a data processor implementation.
	/// </summary>
	/// <typeparam name="TProcessor">The data processor type to register.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	IDataProcessingBuilder AddProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>()
		where TProcessor : class, IDataProcessor;

	/// <summary>
	/// Registers a record handler implementation.
	/// </summary>
	/// <typeparam name="THandler">The record handler type to register.</typeparam>
	/// <typeparam name="TRecord">The record type handled by the handler.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	IDataProcessingBuilder AddRecordHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRecord>()
		where THandler : class, IRecordHandler<TRecord>;

	/// <summary>
	/// Enables a background hosted service that polls for pending data tasks.
	/// Replaces the separate <c>EnableDataProcessingBackgroundService()</c> call.
	/// </summary>
	/// <param name="configure">Optional configuration action for hosted service options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IDataProcessingBuilder EnableBackgroundProcessing(
		Action<DataProcessingHostedServiceOptions>? configure = null);
}
