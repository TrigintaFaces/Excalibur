// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Inbox;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Oracle;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Oracle provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderOracleExtensions
{
	/// <summary>
	/// Configures the inbox to use Oracle storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Configuration action for the Oracle inbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburInbox(inbox =&gt;
	/// {
	///     inbox.UseOracle(oracle =&gt;
	///     {
	///         oracle.ConnectionString("User Id=app;Password=...;Data Source=//localhost:1521/FREEPDB1")
	///               .SchemaName("APP")
	///               .TableName("INBOX_MESSAGES");
	///     });
	/// });
	/// </code>
	/// </example>
	/// <remarks>
	/// To populate the options from configuration, call
	/// <c>services.AddOptions&lt;OracleInboxOptions&gt;().BindConfiguration("Section:Path")</c>
	/// after this registration. Configuration binding is reflective, so it is not trim- or
	/// native-AOT-safe; doing it at your own call site puts the warning where the trimmer can see it
	/// rather than on this method, which is otherwise trim-safe.
	/// </remarks>
	public static IInboxBuilder UseOracle(
		this IInboxBuilder builder,
		Action<IOracleInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new OracleInboxOptions();
		var oracleBuilder = new OracleInboxBuilder(options);
		configure(oracleBuilder);

		var connectionFactory = ResolveConnectionFactory(oracleBuilder);
		var hasBuilderConnection = oracleBuilder.ConnectionFactoryFunc is not null
			|| oracleBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	private static Func<IServiceProvider, Func<OracleConnection>> ResolveConnectionFactory(
		OracleInboxBuilder oracleBuilder)
	{
		if (oracleBuilder.ConnectionFactoryFunc is not null)
		{
			return oracleBuilder.ConnectionFactoryFunc;
		}

		if (oracleBuilder.ConnectionStringNameValue is not null)
		{
			var connectionStringName = oracleBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connectionStringName)
					?? throw new InvalidOperationException(
						$"Connection string '{connectionStringName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new OracleConnection(resolved);
			};
		}

		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<OracleInboxOptions>>();
			return () => new OracleConnection(opts.Value.ConnectionString);
		};
	}

	private static void RegisterOptionsAndServices(
		IInboxBuilder builder,
		OracleInboxOptions options,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<OracleInboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.SchemaName = options.SchemaName;
			opt.TableName = options.TableName;
			opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
			opt.MaxRetryCount = options.MaxRetryCount;
		});

		// The identifier allowlist on schema/table names is enforced for parity with the
		// options-based registration path (OracleInboxExtensions) — otherwise an identifier
		// supplied via SchemaName()/TableName() would reach SQL text unvalidated. HasBuilderConnection
		// suppresses only the connection-string arm, which a builder connection supersedes.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OracleInboxOptions>>(
				new OracleInboxOptionsValidator { HasBuilderConnection = hasBuilderConnection }));
		builder.Services.AddOptions<OracleInboxOptions>().ValidateOnStart();

		// The store's remaining constructor dependency, so this entry point can build its own store rather
		// than only working in hosts that happen to have composed logging already. TryAdd-based, so a host
		// that configures its own logging still wins.
		_ = builder.Services.AddLogging();

		// The fail-closed single-tenant default guarantees a non-null ITenantContext for the tenant
		// predicate; the multi-tenancy composition replaces it with the ambient context.
		builder.Services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext so the documented UseOracle()
		// path applies the tenant predicate, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<IInboxStore> marker inseparably (no lying marker).
		builder.Services.AddTenantAwareStore<IInboxStore, OracleInboxStore>(sp =>
		{
			var factory = connectionFactory(sp);
			var inboxOptions = sp.GetRequiredService<IOptions<OracleInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<OracleInboxStore>>();
			return new OracleInboxStore(
				factory,
				inboxOptions,
				logger,
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>());
		});
		builder.Services.AddKeyedSingleton<IInboxStore>(
			"oracle", (sp, _) => sp.GetRequiredService<OracleInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<IInboxStore>("oracle"));
		builder.Services.AddInboxSchemaValidation();
		builder.Services.AddSingleton<IInboxSchemaValidator>(sp => sp.GetRequiredService<OracleInboxStore>());
	}
}
