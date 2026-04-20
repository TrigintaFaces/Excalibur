// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Excalibur.Dispatch.Serialization.MessagePack;
using Excalibur.Dispatch.Serialization.Protobuf;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Redis;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Outbox.SqlServer;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;


using Excalibur.Security.Configuration;namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Per-package DI registration smoke tests per spec §4.3.
/// Each shipping package's primary Add*() method is called on a clean ServiceCollection + AddLogging().
/// Record.Exception must be null -- proving each package registers without errors.
/// Packages that are abstractions-only (no Add* extension) are proven by compilation of this project.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Component", "Platform")]
public sealed class PackageDiSmokeTests
{
	/// <summary>
	/// Verifies that each package's primary DI registration succeeds without throwing.
	/// </summary>
	[Theory]
	[MemberData(nameof(AllPackageRegistrationsData))]
	public void Package_Registers_Without_Exceptions(string packageName)
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var register = GetRegistration(packageName);

		// Act
		var exception = Record.Exception(() => register(services));

		// Assert
		exception.ShouldBeNull($"Package '{packageName}' DI registration failed");
	}

	/// <summary>
	/// Verifies that each package's DI registration produces a buildable ServiceProvider.
	/// This catches deferred resolution failures (missing dependencies, circular refs).
	/// </summary>
	[Theory]
	[MemberData(nameof(AllPackageRegistrationsData))]
	public void Package_Builds_ServiceProvider_Without_Exceptions(string packageName)
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var register = GetRegistration(packageName);
		register(services);

		// Act
		var exception = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider();
		});

		// Assert
		exception.ShouldBeNull($"Package '{packageName}' ServiceProvider build failed");
	}

	// ───────────────────────────────────────────────────────────────────
	// MemberData: all shipping packages with DI registration methods
	// ───────────────────────────────────────────────────────────────────

	private static readonly IConfiguration EmptyConfiguration =
		new ConfigurationBuilder().AddInMemoryCollection().Build();

	private const string MockConnectionString = "Server=smoke-test;Database=smoke;Trusted_Connection=true";
	private const string MockPostgresConnectionString = "Host=smoke-test;Database=smoke;Username=smoke;Password=smoke";

	private static readonly Lazy<IReadOnlyDictionary<string, Action<IServiceCollection>>> RegistrationMap =
		new(() => AllPackageRegistrations().ToDictionary(static x => x.PackageName, static x => x.Register, StringComparer.Ordinal));

	public static TheoryData<string> AllPackageRegistrationsData => CreateAllPackageRegistrationsData();

	internal static Action<IServiceCollection> GetRegistration(string packageName) => RegistrationMap.Value[packageName];

	public static IEnumerable<(string PackageName, Action<IServiceCollection> Register)> AllPackageRegistrations()
	{
		// ══════════════════════════════════════════════════════════
		// DISPATCH CORE PACKAGES (Excalibur.Dispatch)
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch", s => s.AddDispatch());
		yield return Reg("Excalibur.Dispatch [Pipeline]", s => s.AddDispatchPipeline());
		yield return Reg("Excalibur.Dispatch [Handlers]", s => s.AddDispatchHandlers());
		yield return Reg("Excalibur.Dispatch [Serializer]", s => s.AddDispatchSerializer());
		yield return Reg("Excalibur.Dispatch [PluggableSerialization]", s => s.AddPluggableSerialization());
		yield return Reg("Excalibur.Dispatch [EventSerializer]", s => s.AddEventSerializer());
		yield return Reg("Excalibur.Dispatch [Routing]", s => s.AddDispatchRouting());
		yield return Reg("Excalibur.Dispatch [MessageMapping]", s => s.AddMessageMapping());
		yield return Reg("Excalibur.Dispatch [Telemetry]", s => s.AddDispatchTelemetry());
		yield return Reg("Excalibur.Dispatch [TelemetryProduction]", s => s.AddDispatchTelemetryForProduction());
		yield return Reg("Excalibur.Dispatch [TelemetryDev]", s => s.AddDispatchTelemetryForDevelopment());
		yield return Reg("Excalibur.Dispatch [TelemetryThroughput]", s => s.AddDispatchTelemetryForThroughput());
		yield return Reg("Excalibur.Dispatch [Validation]", s => s.AddDispatchValidation());
		yield return Reg("Excalibur.Dispatch [Upcasting]", s => s.AddMessageUpcasting());
		yield return Reg("Excalibur.Dispatch [Scheduling]", s => s.AddDispatchScheduling());
		yield return Reg("Excalibur.Dispatch [DefaultPipelines]", s => s.AddDefaultDispatchPipelines());
		yield return Reg("Excalibur.Dispatch [UpcastingDecorator]", s => s.AddUpcastingMessageBusDecorator());
		yield return Reg("Excalibur.Dispatch [TimeAwareScheduling]", s => s.AddTimeAwareScheduling());
		yield return Reg("Excalibur.Dispatch [AotCloudEvents]", s => s.AddCloudEventsAotSerialization());
		yield return Reg("Excalibur.Dispatch [AotCore]", s => s.AddCoreAotSerialization());
		yield return Reg("Excalibur.Dispatch [InMemoryDLQ]", s => s.AddInMemoryDeadLetterStore());
		yield return Reg("Excalibur.Dispatch [TimePolicy]", s => s.AddTimePolicy());
		yield return Reg("Excalibur.Dispatch [SystemTimeProvider]", s => s.AddSystemTimeProvider());
		yield return Reg("Excalibur.Dispatch [Threading]", s => s.AddDispatchThreading());
		yield return Reg("Excalibur.Dispatch [InMemoryTransport]", s =>
		{
			s.AddDispatch();
			s.AddInMemoryTransport();
		});

		// ══════════════════════════════════════════════════════════
		// DISPATCH OBSERVABILITY
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Observability", s =>
		{
			s.AddDispatch();
			s.AddDispatchObservability();
		});
		yield return Reg("Excalibur.Dispatch.Observability [AllMetrics]", s => s.AddAllDispatchMetrics());
		yield return Reg("Excalibur.Dispatch.Observability [DispatchMetrics]", s => s.AddDispatchMetricsInstrumentation());
		yield return Reg("Excalibur.Dispatch.Observability [CircuitBreakerMetrics]", s => s.AddCircuitBreakerMetrics());
		yield return Reg("Excalibur.Dispatch.Observability [DLQMetrics]", s => s.AddDeadLetterQueueMetrics());
		yield return Reg("Excalibur.Dispatch.Observability [W3C]", s => s.AddW3CTracingPropagator());
		yield return Reg("Excalibur.Dispatch.Observability [B3]", s => s.AddB3TracingPropagator());

		// ══════════════════════════════════════════════════════════
		// DISPATCH RESILIENCE
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Resilience.Polly", s => s.AddPollyResilience());

		// ══════════════════════════════════════════════════════════
		// DISPATCH SECURITY
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Security", s =>
			s.AddDispatchSecurityMiddleware(
				(Excalibur.Security.Configuration.SecurityOptions opt) => { }));
		yield return Reg("Excalibur.Security.Azure", s =>
			s.AddDispatchSecurityAzure(azure => azure.VaultUri("https://test.vault.azure.net")));

		// ══════════════════════════════════════════════════════════
		// DISPATCH CACHING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Caching", s => s.AddDispatchCaching());

		// ══════════════════════════════════════════════════════════
		// DISPATCH AUDIT LOGGING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.AuditLogging", s => s.AddAuditLogging());
		yield return Reg("Excalibur.AuditLogging [RBAC]", s =>
		{
			s.AddAuditLogging();
			s.AddRbacAuditStore();
		});
		yield return Reg("Excalibur.AuditLogging.Aws", s =>
			s.AddAwsAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.Datadog", s =>
			s.AddDatadogAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.Elasticsearch", s =>
			s.AddElasticsearchAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.GoogleCloud", s =>
			s.AddGoogleCloudAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.Postgres", s =>
			PostgresAuditServiceCollectionExtensions.AddPostgresAuditStore(
				s, (Excalibur.AuditLogging.Postgres.PostgresAuditOptions opt) => { }));
		yield return Reg("Excalibur.AuditLogging.Sentinel", s =>
			s.AddSentinelAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.Splunk", s =>
			s.AddSplunkAuditExporter(_ => { }));
		yield return Reg("Excalibur.AuditLogging.SqlServer", s =>
			s.AddSqlServerAuditStore(_ => { }));

		// ══════════════════════════════════════════════════════════
		// DISPATCH COMPLIANCE
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Compliance [Erasure]", s => s.AddCascadeErasure());
		yield return Reg("Excalibur.Compliance [InMemoryErasureStore]", s => s.AddInMemoryErasureStore());
		yield return Reg("Excalibur.Compliance [LegalHold]", s => s.AddLegalHoldService());
		yield return Reg("Excalibur.Compliance [InMemoryLegalHold]", s => s.AddInMemoryLegalHoldStore());
		yield return Reg("Excalibur.Compliance [DataInventory]", s => s.AddDataInventoryService());
		yield return Reg("Excalibur.Compliance [InMemoryDataInventory]", s => s.AddInMemoryDataInventoryStore());
		yield return Reg("Excalibur.Compliance [ErasureVerification]", s => s.AddErasureVerificationService());
		yield return Reg("Excalibur.Compliance [Metrics]", s => s.AddComplianceMetrics());
		yield return Reg("Excalibur.Compliance [SOC2Store]", s => s.AddInMemorySoc2ReportStore());
		yield return Reg("Excalibur.Compliance [SOC2Monitoring]", s => s.AddSoc2ContinuousMonitoring());
		yield return Reg("Excalibur.Compliance [PciDss]", s => s.AddPciDssDataMasking());
		yield return Reg("Excalibur.Compliance [Hipaa]", s => s.AddHipaaDataMasking());
		yield return Reg("Excalibur.Compliance [StrictMasking]", s => s.AddStrictDataMasking());
		yield return Reg("Excalibur.Compliance [DevEncryption]", s => s.AddDevEncryption());
		yield return Reg("Excalibur.Compliance [FIPS]", s => s.AddFipsValidation());
		yield return Reg("Excalibur.Compliance.Aws", s =>
			s.AddAwsKmsKeyManagement(_ => { }));
		yield return Reg("Excalibur.Compliance.Azure", s =>
			s.AddAzureKeyVaultKeyManagement(_ => { }));
		yield return Reg("Excalibur.Compliance.Vault", s =>
			s.AddVaultKeyManagement(_ => { }));

		// ══════════════════════════════════════════════════════════
		// DISPATCH SERIALIZATION
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Serialization.MessagePack", s =>
			s.AddMessagePackSerializer());
		yield return Reg("Excalibur.Dispatch.Serialization.Protobuf", s =>
			s.AddProtobufSerializer());

		// ══════════════════════════════════════════════════════════
		// DISPATCH TRANSPORT
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Transport.RabbitMQ", s =>
		{
			s.AddDispatch();
			s.AddRabbitMQTransport(rmq => rmq.HostName("localhost"));
		});
		yield return Reg("Excalibur.Dispatch.Transport.Kafka", s =>
		{
			s.AddDispatch();
			s.AddKafkaTransport("kafka-smoke", kafka => kafka.BootstrapServers("localhost:9092"));
		});
		yield return Reg("Excalibur.Dispatch.Transport.Kafka [OtelMetrics]", s => s.AddKafkaOtelMetrics());
		yield return Reg("Excalibur.Dispatch.Transport.AzureServiceBus", s =>
		{
			s.AddDispatch();
			s.AddAzureServiceBusTransport("asb-smoke", asb =>
				asb.ConnectionString("Endpoint=sb://smoke.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"));
		});
		yield return Reg("Excalibur.Dispatch.Transport.AwsSqs", s =>
		{
			s.AddDispatch();
			s.AddAwsSqsTransport("sqs-smoke", _ => { });
		});
		yield return Reg("Excalibur.Dispatch.Transport.GooglePubSub", s =>
		{
			s.AddDispatch();
			s.AddGooglePubSubTransport("pubsub-smoke", ps => ps.ProjectId("smoke-project"));
		});
		yield return Reg("Excalibur.Dispatch.Transport.Grpc", s =>
		{
			s.AddDispatch();
			s.AddGrpcTransport(_ => { });
		});

		// ══════════════════════════════════════════════════════════
		// DISPATCH PATTERNS
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Patterns.ClaimCheck.InMemory", s => s.AddInMemoryClaimCheck());
		yield return Reg("Excalibur.Dispatch.Patterns.Hosting.Json", s => s.AddDispatchPatternsClaimCheckJson());
		yield return Reg("Excalibur.Dispatch.Patterns.Azure", s =>
			s.AddAzureBlobClaimCheck(opt => opt.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=smoke"));

		// ══════════════════════════════════════════════════════════
		// DISPATCH CLAIMCHECK PROVIDERS
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.ClaimCheck.AwsS3", s =>
			s.AddAwsS3ClaimCheck(_ => { }));
		yield return Reg("Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage", s =>
			s.AddGcsClaimCheck(gcs => gcs.BucketName("test")));

		// ══════════════════════════════════════════════════════════
		// DISPATCH TESTING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Testing", s =>
		{
			s.AddDispatch();
			s.AddDispatchTesting();
		});

		// ══════════════════════════════════════════════════════════
		// DISPATCH HOSTING (Serverless)
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Hosting.AwsLambda", s => s.AddAwsLambdaServerless());
		yield return Reg("Excalibur.Dispatch.Hosting.AzureFunctions", s => s.AddAzureFunctionsServerless());
		yield return Reg("Excalibur.Dispatch.Hosting.GoogleCloudFunctions", s => s.AddGoogleCloudFunctionsServerless());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR DOMAIN & DATA
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Domain [BoundedContext]", s => s.AddBoundedContextEnforcement());
		// S804 bd-sdhocq A6: AddExcaliburDataServices deleted. Data wiring is via AddExcalibur root.
		yield return Reg("Excalibur.Data", s => s.AddExcalibur(_ => { }));
		yield return Reg("Excalibur.Data [Persistence]", s => s.AddPersistence());
		yield return Reg("Excalibur.Data.InMemory [SnapshotStore]", s => s.AddInMemorySnapshotStore());
		yield return Reg("Excalibur.Data.InMemory [InboxStore]", s => s.AddInMemoryInboxStore());
		yield return Reg("Excalibur.Data.InMemory [OutboxStore]", s => s.AddInMemoryOutboxStore());
		yield return Reg("Excalibur.Data.MySql", s =>
			s.AddExcaliburMySql(_ => { }));
		yield return Reg("Excalibur.Data.SqlServer", s => s.AddExcaliburSqlServices());
		yield return Reg("Excalibur.Data.SqlServer [CdcProcessor]", s => s.AddCdcProcessor());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR DATA (ElasticSearch)
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Data.ElasticSearch [FieldEncryption]", s => s.AddFieldEncryption());
		yield return Reg("Excalibur.Data.ElasticSearch [LocalKeyProvider]", s => s.AddLocalKeyProvider());
		yield return Reg("Excalibur.Data.ElasticSearch [SecurityAuditing]", s => s.AddSecurityAuditing());
		yield return Reg("Excalibur.Data.ElasticSearch [SecurityMonitoring]", s => s.AddSecurityMonitoring());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR EVENT SOURCING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.EventSourcing", s => s.AddExcalibur(x => x.AddEventSourcing()));
		yield return Reg("Excalibur.EventSourcing [MaterializedViews]", s => s.AddMaterializedViews());
		yield return Reg("Excalibur.EventSourcing [SnapshotUpgrader]", s => s.AddSnapshotUpgraderRegistry());
		yield return Reg("Excalibur.EventSourcing [SnapshotEncryption]", s => s.AddSnapshotEncryption());
		yield return Reg("Excalibur.EventSourcing [SnapshotCompression]", s => s.AddSnapshotCompression());
		yield return Reg("Excalibur.EventSourcing [SnapshotVersioning]", s => s.AddSnapshotSchemaVersioning());
		yield return Reg("Excalibur.EventSourcing [TimeTravel]", s => s.AddTimeTravelQuery());
		yield return Reg("Excalibur.EventSourcing.InMemory", s => s.AddInMemoryEventStore());
		yield return Reg("Excalibur.EventSourcing.SqlServer", s =>
			s.AddSqlServerEventStore(() => new Microsoft.Data.SqlClient.SqlConnection(MockConnectionString)));
		yield return Reg("Excalibur.EventSourcing.Postgres", s =>
			s.AddExcalibur(x => x.AddEventSourcing(es =>
				es.UsePostgres(pg => pg.ConnectionString(MockPostgresConnectionString)))));
		yield return Reg("Excalibur.EventSourcing.CosmosDb", s =>
			s.AddExcalibur(x => x.AddEventSourcing(es =>
				es.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://localhost:8081;AccountKey=smoke==")))));
		yield return Reg("Excalibur.EventSourcing.DynamoDb", s =>
			s.AddExcalibur(x => x.AddEventSourcing(es =>
				es.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")))));
		yield return Reg("Excalibur.EventSourcing.Firestore", s =>
			s.AddExcalibur(x => x.AddEventSourcing(es =>
				es.UseFirestore(fs => fs.ProjectId("smoke-project")))));
		yield return Reg("Excalibur.EventSourcing.Redis", s =>
			s.AddExcalibur(x => x.AddEventSourcing(es =>
				es.UseRedis(redis => redis.ConnectionString("localhost:6379")))));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR OUTBOX
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Outbox", s => s.AddExcalibur(x => x.AddOutbox(_ => { })));
		yield return Reg("Excalibur.Outbox [HostedService]", s => s.AddOutboxHostedService());
		yield return Reg("Excalibur.Outbox [InboxHostedService]", s => s.AddInboxHostedService());
		yield return Reg("Excalibur.Outbox.SqlServer", s =>
			s.AddSqlServerOutboxStore(opts => opts.ConnectionString = MockConnectionString));
		yield return Reg("Excalibur.Outbox.CosmosDb", s =>
			s.AddExcalibur(x => x.AddOutbox(outbox =>
				outbox.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://localhost:8081;AccountKey=smoke==")))));
		yield return Reg("Excalibur.Outbox.DynamoDb", s =>
			s.AddExcalibur(x => x.AddOutbox(outbox =>
				outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")))));
		yield return Reg("Excalibur.Outbox.Firestore", s =>
			s.AddExcalibur(x => x.AddOutbox(outbox =>
				outbox.UseFirestore(fs => fs.ProjectId("smoke-project")))));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR LEADER ELECTION
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.LeaderElection", s => s.AddExcalibur(x => x.AddLeaderElection(_ => { })));
		yield return Reg("Excalibur.LeaderElection [Telemetry]", s => s.AddLeaderElectionTelemetry());
		yield return Reg("Excalibur.LeaderElection [HealthCheck]", s => s.AddLeaderElectionHealthCheck());
		yield return Reg("Excalibur.LeaderElection [Watcher]", s => s.AddLeaderElectionWatcher());
		yield return Reg("Excalibur.LeaderElection.InMemory", s => s.AddInMemoryLeaderElection());
		yield return Reg("Excalibur.LeaderElection.Redis", s =>
			s.AddExcalibur(x => x.AddLeaderElection(le =>
				le.UseRedis(redis => redis
					.ConnectionString("localhost:6379")
					.LockKey("smoke-lock")))));
		yield return Reg("Excalibur.LeaderElection.SqlServer", s =>
			s.AddExcalibur(x => x.AddLeaderElection(le =>
				le.UseSqlServer(sql => sql
					.ConnectionString(MockConnectionString)
					.LockResource("smoke-lock")))));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR SAGA
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Saga", s => s.AddExcalibur(x => x.AddSagas()));
		yield return Reg("Excalibur.Saga [Timeout]", s => s.AddSagaTimeoutDelivery());
		yield return Reg("Excalibur.Saga [Instrumentation]", s => s.AddSagaInstrumentation());
		yield return Reg("Excalibur.Saga [Correlation]", s => s.AddSagaCorrelation());
		yield return Reg("Excalibur.Saga [StateInspection]", s => s.AddSagaStateInspection());
		yield return Reg("Excalibur.Saga [Reminders]", s => s.AddSagaReminders());
		yield return Reg("Excalibur.Saga [Snapshots]", s => s.AddSagaSnapshots());
		yield return Reg("Excalibur.Saga [TimeoutCleanup]", s => s.AddSagaTimeoutCleanup());
		yield return Reg("Excalibur.Saga [Orchestration]", s => s.AddDispatchOrchestration());
		yield return Reg("Excalibur.Saga.SqlServer", s =>
			s.AddSqlServerSagaStore(sql => { sql.ConnectionString = MockConnectionString; }));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR A3 (Authentication, Authorization, Auditing)
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.A3", s => s.AddExcaliburA3());
		yield return Reg("Excalibur.A3 [DispatchServices]", s => s.AddA3DispatchServices());
		yield return Reg("Excalibur.A3 [Authorization]", s => s.AddDispatchAuthorization());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR APPLICATION
		// ══════════════════════════════════════════════════════════

		// S804 bd-sdhocq A7: AddExcaliburApplicationServices deleted. Use AddExcalibur + ScanAssemblies.
		yield return Reg("Excalibur.Application", s => s.AddExcalibur(b => b.ScanAssemblies(typeof(PackageDiSmokeTests).Assembly)));
		yield return Reg("Excalibur.Application [Activities]", s => s.AddActivities());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR CACHING & CDC
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Caching [Projections]", s => s.AddExcaliburProjectionCaching());
		yield return Reg("Excalibur.Cdc", s => s.AddCdcProcessor());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR COMPLIANCE
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Compliance.Postgres", s =>
			s.AddPostgresErasureStore(MockConnectionString));
		yield return Reg("Excalibur.Compliance.SqlServer", s =>
			s.AddSqlServerKeyEscrow(opts => opts.ConnectionString = MockConnectionString));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR HOSTING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Hosting", s => s.AddExcalibur(_ => { }));
		// S804 bd-sdhocq A8: AddExcaliburBaseServices deleted. Replaced by AddExcalibur + ScanAssemblies.
		yield return Reg("Excalibur.Hosting [BaseServices]", s =>
			s.AddExcalibur(b => b.ScanAssemblies(Array.Empty<Assembly>())));
		yield return Reg("Excalibur.Hosting.HealthChecks", s => s.AddExcaliburHealthChecks());
		// S804 bd-sdhocq A9: AddExcaliburWebServices deleted. Web hosting wires via AddExcalibur
		// + explicit API versioning opt-in (not bundled at composition root).
		yield return Reg("Excalibur.Hosting.Web", s => s.AddGlobalExceptionHandler());
		yield return Reg("Excalibur.Hosting.AwsLambda [Excalibur]", s => s.AddExcaliburAwsLambdaServerless());
		yield return Reg("Excalibur.Hosting.AzureFunctions [Excalibur]", s => s.AddExcaliburAzureFunctionsServerless());
		yield return Reg("Excalibur.Hosting.GoogleCloudFunctions [Excalibur]", s => s.AddExcaliburGoogleCloudFunctionsServerless());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR SECURITY
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Security [PasswordHasher]", s => s.AddPasswordHasher());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR TESTING
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Testing [Stores]", s => s.AddExcaliburTestingStores());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR JOBS
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Jobs.Aws", s =>
			s.AddAwsScheduler(_ => { }));
		yield return Reg("Excalibur.Jobs.Azure", s =>
			s.AddAzureLogicApps(_ => { }));
		yield return Reg("Excalibur.Jobs.GoogleCloud", s =>
			s.AddGoogleCloudScheduler(_ => { }));
		yield return Reg("Excalibur.Jobs.Redis", s =>
			s.AddJobCoordinationRedis("localhost:6379"));
		yield return Reg("Excalibur.Jobs.SqlServer", s =>
			s.AddSqlServerJobCoordinator(_ => { }));
	}

	private static TheoryData<string> CreateAllPackageRegistrationsData()
	{
		var data = new TheoryData<string>();

		foreach (var (packageName, _) in AllPackageRegistrations())
		{
			data.Add(packageName);
		}

		return data;
	}

	private static (string PackageName, Action<IServiceCollection> Register) Reg(string name, Action<IServiceCollection> register)
		=> (name, register);
}
