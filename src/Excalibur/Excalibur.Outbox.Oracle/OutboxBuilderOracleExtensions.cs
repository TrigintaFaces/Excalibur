// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0




namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Extension methods for configuring Oracle provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// Connection is configured via the builder using
/// <see cref="IOracleOutboxBuilder.ConnectionString"/> or
/// <see cref="IOracleOutboxBuilder.ConnectionFactory"/>.
/// </para>
/// </remarks>
public static class OutboxBuilderOracleExtensions
{
	/// <summary>
	/// Configures the outbox to use Oracle as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure Oracle-specific options including connection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Oracle as the outbox storage provider.
	/// It registers the <see cref="OracleOutboxStore"/> and related services.
	/// Connection can be provided via the builder using
	/// <see cref="IOracleOutboxBuilder.ConnectionString"/> or
	/// <see cref="IOracleOutboxBuilder.ConnectionFactory"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseOracle(oracle =>
	///     {
	///         oracle.ConnectionString(connectionString)
	///                 .SchemaName("messaging")
	///                 .TableName("outbox_messages")
	///                 .ReservationTimeout(TimeSpan.FromMinutes(10))
	///                 .MaxAttempts(5);
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
	///
	/// // IDb factory
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseOracle(oracle =>
	///     {
	///         oracle.ConnectionFactory(sp => sp.GetRequiredService&lt;IDb&gt;())
	///                 .SchemaName("messaging");
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </example>
	public static IOutboxBuilder UseOracle(
		this IOutboxBuilder builder,
		Action<IOracleOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure Oracle options
		var oracleOptions = new OracleOutboxStoreOptions();
		var oracleBuilder = new OracleOutboxBuilder(oracleOptions);
		configure(oracleBuilder);

		OracleOutboxRegistration.Register(builder, oracleBuilder, oracleOptions);

		return builder;
	}
}
