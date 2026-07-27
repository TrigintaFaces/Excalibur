// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle.DependencyInjection;

/// <summary>
/// Extension methods for configuring Oracle saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderOracleExtensions
{
	/// <summary>
	/// Configures the saga builder to use Oracle for saga store and timeout store.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure the Oracle saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ISagaBuilder UseOracle(
		this ISagaBuilder builder,
		Action<IOracleSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new OracleSagaStoreOptions();
		var oracleBuilder = new OracleSagaBuilder(options);
		configure(oracleBuilder);

		var connectionFactory = ResolveConnectionFactory(oracleBuilder);
		var hasBuilderConnection = oracleBuilder.ConnectionFactoryFunc is not null
			|| oracleBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, oracleBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	private static Func<IServiceProvider, Func<OracleConnection>> ResolveConnectionFactory(
		OracleSagaBuilder oracleBuilder)
	{
		if (oracleBuilder.ConnectionFactoryFunc is not null)
		{
			return oracleBuilder.ConnectionFactoryFunc;
		}

		if (oracleBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = oracleBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new OracleConnection(resolved);
			};
		}

		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			return () => new OracleConnection(opts.Value.ConnectionString);
		};
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ISagaBuilder builder,
		OracleSagaBuilder oracleBuilder,
		OracleSagaStoreOptions options,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<OracleSagaStoreOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.SchemaName = options.SchemaName;
			opt.TableName = options.TableName;
		});

		if (oracleBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<OracleSagaStoreOptions>()
				.BindConfiguration(oracleBuilder.BindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<OracleSagaStoreOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		builder.Services.AddSingleton<IValidateOptions<OracleSagaStoreOptions>>(
			new OracleSagaBuilderOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<OracleSagaStoreOptions>().ValidateOnStart();

		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var storeOptions = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaStore>>();
			var serializer = sp.GetRequiredService<Excalibur.Dispatch.Serialization.DispatchJsonSerializer>();
			return new OracleSagaStore(factory, storeOptions, logger, serializer, sp.GetService<ITenantContext>());
		});
		builder.Services.AddKeyedSingleton<ISagaStore>(
			"oracle", (sp, _) => sp.GetRequiredService<OracleSagaStore>());
		builder.Services.TryAddKeyedSingleton<ISagaStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<ISagaStore>("oracle"));

		// Timeout store sharing the same connection.
		_ = builder.Services.Configure<OracleSagaTimeoutStoreOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
		});
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			var timeoutOptions = sp.GetRequiredService<IOptions<OracleSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaTimeoutStore>>();
			return new OracleSagaTimeoutStore(factory, timeoutOptions, logger);
		});
		builder.Services.TryAddSingleton<ISagaTimeoutStore>(
			sp => sp.GetRequiredService<OracleSagaTimeoutStore>());
	}
}
