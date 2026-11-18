// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Oracle.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>One provider-local configuration snapshot; no global IDb or credential extraction.</summary>
internal static partial class OracleOutboxRegistration
{
	internal static void Register(IOutboxBuilder builder, OracleOutboxBuilder provider, OracleOutboxStoreOptions configured)
	{
		var snapshot = Clone(configured);
		var directString = provider.ConfiguredConnectionString;
		var connectionName = provider.ConnectionStringNameValue;
		var configurationPath = provider.BindConfigurationPath;
		var borrowedFactory = provider.ConfiguredDbFactory;
		var workerFactory = provider.ConfiguredWorkerFactory;
		var overrides = provider.ConfigurationOverrides.ToArray();
		if (directString is null && connectionName is null && configurationPath is null && borrowedFactory is null)
		{
			throw new InvalidOperationException("Oracle outbox requires ConnectionString(), ConnectionStringName(), BindConfiguration(), or ConnectionFactory().");
		}

		ConfigureOptions(builder.Services, snapshot, configurationPath, overrides);
		builder.Services.AddDefaultTenantContext();

		RegisterStore(builder.Services, directString, connectionName, configurationPath, borrowedFactory, workerFactory);
		RegisterAliases(builder.Services);
	}

	private static void ConfigureOptions(IServiceCollection services, OracleOutboxStoreOptions snapshot,
		string? configurationPath, IEnumerable<Action<OracleOutboxStoreOptions>> overrides)
	{
		var options = services.AddOptions<OracleOutboxStoreOptions>()
			.Configure(value => ApplyBuilderSettings(snapshot, value));
		if (configurationPath is not null)
		{
			options.BindConfiguration(configurationPath);
			options.Configure(value =>
			{
				foreach (var apply in overrides) { apply(value); }
			});
		}
		options.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OracleOutboxStoreOptions>, OracleOutboxStoreOptionsValidator>());
	}

	private static void RegisterStore(IServiceCollection registrations, string? directString, string? connectionName,
		string? configurationPath, Func<IServiceProvider, IDb>? borrowedFactory,
		Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>>? workerFactory)
	{
		registrations.AddTenantAwareStore<IOutboxStore, OracleOutboxStore>(services =>
		{
			var selectedOptions = Clone(services.GetRequiredService<IOptions<OracleOutboxStoreOptions>>().Value);
			var logger = services.GetRequiredService<ILogger<OracleOutboxStore>>();
			var metrics = services.GetService<OracleOutboxStoreMetrics>();
			var selectedString = directString;
			if (connectionName is not null)
			{
				selectedString = services.GetRequiredService<IConfiguration>().GetConnectionString(connectionName);
				if (string.IsNullOrWhiteSpace(selectedString))
				{
					throw new InvalidOperationException($"Oracle outbox connection-string name '{connectionName}' was not configured.");
				}
			}
			if (configurationPath is not null)
			{
				selectedString = services.GetRequiredService<IConfiguration>().GetSection(configurationPath)["ConnectionString"];
				if (string.IsNullOrWhiteSpace(selectedString))
				{
					throw new InvalidOperationException($"Oracle outbox configuration section '{configurationPath}' requires ConnectionString.");
				}
			}

			var workerSource = workerFactory is not null
				? OracleWorkerConnectionSource.FromFactory(services.GetRequiredService<IServiceScopeFactory>(), workerFactory)
				: selectedString is not null ? OracleWorkerConnectionSource.FromConnectionString(selectedString) : null;

			// A caller-provided IDb remains borrowed. String-derived staging belongs to THIS store,
			// not an unrelated pre-existing unkeyed IDb and not a DI-owned worker connection.
			var ownsStaging = borrowedFactory is null;
			var db = borrowedFactory is not null
				? borrowedFactory(services)
				: new OutboxDb(new OracleConnection(selectedString!));
			try
			{
				return new OracleOutboxStore(db, Options.Create(selectedOptions), logger, metrics,
					workerConnections: workerSource, ownsStagingDb: ownsStaging);
			}
			catch
			{
				if (ownsStaging && db is IDisposable disposable)
				{
					try { disposable.Dispose(); }
					catch (Exception error) { LogStagingCleanup(logger, error); }
				}
				throw;
			}
		});
	}

	private static void RegisterAliases(IServiceCollection services)
	{
		services.AddKeyedSingleton<IOutboxStore>("oracle", (services, _) => services.GetRequiredService<OracleOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (services, _) => services.GetRequiredKeyedService<IOutboxStore>("oracle"));
		services.TryAddSingleton<ITransactionalOutboxWriter>(services => services.GetRequiredService<OracleOutboxStore>());
	}

	private static OracleOutboxStoreOptions Clone(OracleOutboxStoreOptions source)
	{
		var copy = new OracleOutboxStoreOptions();
		Copy(source, copy);
		return copy;
	}

	// Preserve pre-existing Configure<OracleOutboxStoreOptions> behavior. Only the settings the
	// provider builder actually owns override earlier global configuration; unrelated properties
	// such as MessageTypes, MaxPayloadBytes and FailureBackoffFloorSeconds keep caller configuration.
	private static void ApplyBuilderSettings(OracleOutboxStoreOptions source, OracleOutboxStoreOptions target)
	{
		target.SchemaName = source.SchemaName;
		target.OutboxTableName = source.OutboxTableName;
		target.DeadLetterTableName = source.DeadLetterTableName;
		target.ReservationTimeout = source.ReservationTimeout;
		target.MaxAttempts = source.MaxAttempts;
		target.BatchProcessing.BatchProcessingTimeout = source.BatchProcessing.BatchProcessingTimeout;
	}

	// Explicit typed snapshot: no reflection-based option clone, and no shared mutable batch/list.
	private static void Copy(OracleOutboxStoreOptions source, OracleOutboxStoreOptions target)
	{
		target.SchemaName = source.SchemaName;
		target.OutboxTableName = source.OutboxTableName;
		target.DeadLetterTableName = source.DeadLetterTableName;
		target.FenceTableName = source.FenceTableName;
		target.ReservationTimeout = source.ReservationTimeout;
		target.FailureBackoffFloorSeconds = source.FailureBackoffFloorSeconds;
		target.PerRunTotal = source.PerRunTotal;
		target.QueueCapacity = source.QueueCapacity;
		target.ProducerBatchSize = source.ProducerBatchSize;
		target.ConsumerBatchSize = source.ConsumerBatchSize;
		target.MaxAttempts = source.MaxAttempts;
		target.DefaultMessageTimeToLive = source.DefaultMessageTimeToLive;
		target.EnableBatchDatabaseOperations = source.EnableBatchDatabaseOperations;
		target.DeliveryGuarantee = source.DeliveryGuarantee;
		target.MaxPayloadBytes = source.MaxPayloadBytes;
		target.SingleActiveWriter = source.SingleActiveWriter;
		target.MessageTypes = source.MessageTypes.ToList();
		target.BatchProcessing = new()
		{
			ParallelProcessingDegree = source.BatchProcessing.ParallelProcessingDegree,
			BatchProcessingTimeout = source.BatchProcessing.BatchProcessingTimeout,
			EnableDynamicBatchSizing = source.BatchProcessing.EnableDynamicBatchSizing,
			MinBatchSize = source.BatchProcessing.MinBatchSize,
			MaxBatchSize = source.BatchProcessing.MaxBatchSize,
		};
	}
	[LoggerMessage(OutboxOracleEventId.OutboxStagingSetupCleanup, LogLevel.Warning, "Private Oracle staging cleanup failed after constructor failure.")]
	private static partial void LogStagingCleanup(ILogger logger, Exception error);

}
