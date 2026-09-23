// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Middleware.PipelineDiagnostics;
using Excalibur.Domain;
using Excalibur.Dispatch.Serialization.MessagePack;
using Excalibur.Dispatch.Serialization.Protobuf;
using Excalibur.Dispatch.Threading;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Redis;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Outbox.SqlServer;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;


namespace Excalibur.Dispatch.Tests.Smoke;

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
		var services = CreateHostServices();
		var register = GetRegistration(packageName);

		// Act
		var exception = Record.Exception(() => register(services));

		// Assert
		exception.ShouldBeNull($"Package '{packageName}' DI registration failed");
	}

	/// <summary>
	/// Verifies that each package's DI registration produces a container that can be built.
	/// </summary>
	/// <remarks>
	/// This checks that building the container does not throw. It does NOT check that the registered
	/// services can be constructed: <c>BuildServiceProvider()</c> resolves nothing, so a registration
	/// whose dependency is missing entirely still passes here and fails later, at host start, in the
	/// consumer's process. Constructing every descriptor is what <c>ValidateOnBuild</c> does, and it
	/// is now switched on: every descriptor each package registers must be constructible, and every
	/// scoped service must be resolvable without escaping into the root provider. A package whose
	/// <c>Add*()</c> registers a component it never supplies a dependency for fails here rather than
	/// in the consumer's host at start-up.
	/// </remarks>
	[Theory]
	[MemberData(nameof(AllPackageRegistrationsData))]
	public void Package_Builds_ServiceProvider_Without_Exceptions(string packageName)
	{
		// Arrange
		var services = CreateHostServices();
		var register = GetRegistration(packageName);
		register(services);
		AddConsumerSuppliedSeams(services);

		// Act
		var exception = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider(
				new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
		});

		// Assert
		exception.ShouldBeNull($"Package '{packageName}' ServiceProvider build failed");
	}

	/// <summary>
	/// Verifies that each consumer-supplied entry point actually registers the service it advertises.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The two theories above are <b>liveness-blind</b> for these entry points. An <c>Add*()</c> that takes
	/// the consumer's implementation as a type argument and registers nothing of the framework's own would
	/// pass both: nothing throws, and a container with one fewer descriptor still builds. Deleting the body
	/// of <c>AddGooglePubSubSchemaManager</c> would go entirely undetected -- a case that cannot fail.
	/// </para>
	/// <para>
	/// This is the arm that can. Asserting the descriptor exists is what makes the registration itself
	/// falsifiable; the build below is what makes its <i>dependencies</i> falsifiable. Both are needed:
	/// the first catches an entry point that stopped registering, the second catches one that registers a
	/// component nothing can supply a dependency for.
	/// </para>
	/// </remarks>
	[Theory]
	[MemberData(nameof(AdvertisedServicesData))]
	public void EntryPoint_Registers_Its_Advertised_Service(string caseName, Type serviceType)
	{
		// Arrange
		var services = CreateHostServices();

		// Act
		GetRegistration(caseName)(services);

		// Assert
		services.ShouldContain(
			d => d.ServiceType == serviceType,
			$"'{caseName}' registered no {serviceType} -- the entry point advertises one.");

		AddConsumerSuppliedSeams(services);
		using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
	}

	/// <summary>
	/// Verifies <c>UseTenant</c> contributes a tenant configuration of its own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Bare presence cannot discriminate: <c>AddExcalibur</c> configures <c>TenantContextOptions</c>
	/// itself, so one <c>IConfigureOptions&lt;TenantContextOptions&gt;</c> is there either way. A second
	/// one is what <c>UseTenant</c> adds, so the COUNT is falsifiable where presence is not -- empty out
	/// <c>UseTenant</c> and this drops to one.
	/// </para>
	/// <para>
	/// A count proves the call is wired; it cannot prove the value wins. That is asserted by
	/// <see cref="UseTenant_Value_Is_The_Resolved_Default_Tenant"/>, below.
	/// </para>
	/// </remarks>
	[Fact]
	public void UseTenant_Contributes_A_Tenant_Configuration()
	{
		// Arrange
		var services = CreateHostServices();

		// Act
		GetRegistration("Excalibur.Hosting [BuilderContext]")(services);

		// Assert
		services.Count(d => d.ServiceType == typeof(IConfigureOptions<TenantContextOptions>))
			.ShouldBeGreaterThanOrEqualTo(
				2,
				"UseTenant should add a tenant configuration alongside the one AddExcalibur registers.");
	}

	/// <summary>
	/// Verifies the tenant passed to <c>UseTenant</c> is the default tenant the host actually resolves.
	/// </summary>
	/// <remarks>
	/// <c>AddExcalibur</c> configures <c>TenantContextOptions</c> AFTER the builder callback returns, and
	/// options configuration is last-wins. This arm is what fails if the framework's own default ever
	/// overwrites the value the consumer configured again: it was RED, resolving <c>__default__</c>, until
	/// the framework stopped seating its default over a configured one.
	/// </remarks>
	[Fact]
	public void UseTenant_Value_Is_The_Resolved_Default_Tenant()
	{
		// Arrange
		var services = CreateHostServices();
		GetRegistration("Excalibur.Hosting [BuilderContext]")(services);

		// Act
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<TenantContextOptions>>().Value.DefaultTenantId;

		// Assert
		resolved.ShouldBe("smoke-tenant", "the tenant passed to UseTenant must be the default tenant the host resolves");
	}

	/// <summary>
	/// Verifies <c>UseLocalClientAddress</c> replaces the scoped default with the local-machine singleton.
	/// </summary>
	/// <remarks>
	/// Both the default (<c>TryAddClientAddress</c>, scoped) and the override
	/// (<c>TryAddLocalClientAddress</c>, singleton) register <see cref="IClientAddress"/>, so presence
	/// proves nothing and the LIFETIME is what discriminates. Resolving the instance would make the test
	/// depend on a DNS lookup, which is not what is under test.
	/// </remarks>
	[Fact]
	public void UseLocalClientAddress_Replaces_The_Scoped_Default_With_A_Singleton()
	{
		// Arrange
		var services = CreateHostServices();

		// Act
		GetRegistration("Excalibur.Hosting [BuilderContext]")(services);

		// Assert
		var clientAddress = services.Single(d => d.ServiceType == typeof(IClientAddress));
		clientAddress.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	public static TheoryData<string, Type> AdvertisedServicesData
	{
		get
		{
			var data = new TheoryData<string, Type>();

			foreach (var (caseName, serviceType) in AdvertisedServices())
			{
				data.Add(caseName, serviceType);
			}

			return data;
		}
	}

	/// <summary>
	/// The service each consumer-supplied entry point advertises, paired with the case that calls it.
	/// </summary>
	private static IEnumerable<(string CaseName, Type ServiceType)> AdvertisedServices()
	{
		yield return ("Excalibur.Dispatch.Transport.Abstractions [CloudEventEncoder]",
			typeof(Excalibur.Dispatch.Transport.ICloudEventEncoder<SmokeTransportMessage>));
		yield return ("Excalibur.Dispatch.Transport.Abstractions [CloudEventEncoderFactory]",
			typeof(Excalibur.Dispatch.Transport.ICloudEventEncoder<SmokeTransportMessage>));
		yield return ("Excalibur.Dispatch.Transport.GooglePubSub [SchemaManager]",
			typeof(Excalibur.Dispatch.Transport.Google.IPubSubSchemaManager));
		yield return ("Excalibur.Dispatch.Transport.GooglePubSub [SchemaManagerFactory]",
			typeof(Excalibur.Dispatch.Transport.Google.IPubSubSchemaManager));
		yield return ("Excalibur.Dispatch.Transport.AzureServiceBus [Transactions]",
			typeof(Excalibur.Dispatch.Transport.Azure.IAzureServiceBusTransaction));
		yield return ("Excalibur.Data.DynamoDb [ProjectionStore]",
			typeof(Excalibur.EventSourcing.IProjectionStore<SmokeProjection>));
		yield return ("Excalibur.Data.Firestore [ProjectionStore]",
			typeof(Excalibur.EventSourcing.IProjectionStore<SmokeProjection>));
		yield return ("Excalibur.Caching [ProjectionResolvers]",
			typeof(Excalibur.Caching.Projections.IProjectionTagResolver<SmokeProjectionMessage>));
		yield return ("Excalibur.Caching [ProjectionResolversFromAssembly]",
			typeof(Excalibur.Caching.Projections.IProjectionTagResolver<SmokeProjectionMessage>));
		// Nothing in CreateHostServices registers a hosted service, and this case calls only
		// AddInboxSchemaValidation, so IHostedService presence is specific to that call.
		yield return ("Excalibur.Inbox [SchemaValidation]", typeof(IHostedService));
		yield return ("Excalibur.Operations.Dashboard [Throughput]",
			typeof(Excalibur.Operations.Dashboard.IDashboardEndpointModule));
	}

	/// <summary>
	/// Builds the base composition every package case starts from: logging, the services a real .NET host
	/// always supplies, and a stub for every seam a package legitimately expects the consumer to choose.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Nothing here stands in for a registration a package owns. Each entry is one of three things.
	/// </para>
	/// <para>
	/// <b>Host services.</b> <see cref="IHostEnvironment"/>, <see cref="IConfiguration"/>,
	/// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> and <see cref="IHostApplicationLifetime"/>
	/// are present in every real host -- verified by
	/// enumerating the service descriptors of both <c>Host.CreateApplicationBuilder()</c> and
	/// <c>WebApplication.CreateBuilder()</c> -- and absent only from a bare
	/// <see cref="ServiceCollection"/>. Supplying them removes a difference between this composition and a
	/// host, not a difference between a working package and a broken one.
	/// </para>
	/// <para>
	/// <b>Consumer-chosen seams.</b> A pick-your-provider abstraction -- the outbox publisher, the leader
	/// election, the compliance stores, the distributed cache -- cannot be satisfied by a bare
	/// <c>Add*()</c> without the package choosing infrastructure on the consumer's behalf, which is exactly
	/// what this framework does not do. The stubs are registered through a factory that throws: the factory
	/// is never invoked, it only makes the service type present so a package's own descriptors can be
	/// validated. If one is ever resolved, it throws rather than pretending to work.
	/// </para>
	/// <para>
	/// <b>The harness's own fixtures.</b> <c>AddDispatch</c> and <c>AddExcalibur</c> scan the calling
	/// assembly, which here is the smoke assembly, so its scenario handlers
	/// (<c>PipelineCreateOrderHandler</c>, <c>ValidatedCreateOrderHandler</c>) are registered alongside the
	/// package under test and require the aggregate repository a consumer registers in their own
	/// composition root.
	/// </para>
	/// </remarks>
	private static ServiceCollection CreateHostServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		// --- Services a real host supplies (measured present in both host builders) ---
		_ = services.AddMetrics();
		services.AddSingleton<IConfiguration>(static _ => new ConfigurationBuilder().Build());
		services.AddSingleton<IHostEnvironment>(static _ => new SmokeHostEnvironment());
		services.AddSingleton<IHostApplicationLifetime>(static _ => new SmokeHostApplicationLifetime());

		// --- The harness's own scanned fixtures ---
		// No package registers this, so ordering is irrelevant and it belongs in the base composition.
		Stub<IEventSourcedRepository<PipelineOrderAggregate, Guid>>(services);

		return services;
	}

	/// <summary>
	/// Supplies the seams a package legitimately expects the consumer to choose, for any the package under
	/// test did not register itself.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This runs <b>after</b> the package's own <c>Add*()</c>, and every registration is <c>TryAdd</c>, so a
	/// package that does register one of these keeps its own. Ordering is the whole point: a stub seated
	/// first would silently win every <c>TryAdd</c> in the package under test, and the gate would then go
	/// green for a package that had stopped registering something it owns -- a pass it did not earn.
	/// </para>
	/// <para>
	/// Each entry is a pick-your-provider abstraction -- the outbox publisher, the leader election, the
	/// compliance and audit stores, the distributed cache, the event serializer -- that a bare
	/// <c>Add*()</c> cannot satisfy without choosing infrastructure on the consumer's behalf, which is
	/// exactly what this framework does not do. The stubs are factories that throw: the factory is never
	/// invoked, it only makes the service type present so the package's own descriptors can be validated,
	/// and anything that did resolve one gets an exception rather than a silent substitute.
	/// </para>
	/// </remarks>
	private static void AddConsumerSuppliedSeams(IServiceCollection services)
	{
		// A distributed cache backend is a consumer choice (Redis, SQL Server, memory); the harness picks
		// the in-process one. TryAdd inside AddDistributedMemoryCache, so a package's own still wins.
		_ = services.AddDistributedMemoryCache();

		// The application SCOPE is a second, distinct consumer choice, and supplying the backend above
		// does not supply it. Authorization cache keys identify a user but not an application, so a
		// package needing partitioning depends on the keyed cache and must not silently fall back to the
		// unkeyed one. TryAddKeyedSingleton inside, so a package registering its own still wins.
		_ = services.AddApplicationScopedDistributedCache(o => o.Scope = "package-di-smoke");

		// The payload serializer is a consumer choice too: a transport package registers no serializer,
		// because one that seated a process-wide default would make the wire format depend on which
		// sibling transport was registered first. TryAdd inside AddPluggableSerialization, so a bundle
		// that seats its own still wins.
		_ = services.AddPluggableSerialization();

		TryStub<Excalibur.Dispatch.IOutboxPublisher>(services);
		TryStub<Excalibur.Dispatch.IOutboxStore>(services);
		TryStub<Excalibur.Dispatch.IInboxStore>(services);
		TryStub<Excalibur.Dispatch.Delivery.IScheduleStore>(services);
		TryStub<Excalibur.Dispatch.Messaging.ISagaStore>(services);
		TryStub<Excalibur.Dispatch.LeaderElection.ILeaderElection>(services);
		TryStub<Excalibur.Dispatch.LeaderElection.ILeaderElectionFactory>(services);
		TryStub<Excalibur.EventSourcing.IMaterializedViewStore>(services);
		TryStub<Excalibur.Compliance.IErasureService>(services);
		TryStub<Excalibur.Compliance.IErasureStore>(services);
		TryStub<Excalibur.Compliance.ILegalHoldStore>(services);
		TryStub<Excalibur.Compliance.IDataInventoryStore>(services);
		TryStub<Excalibur.Compliance.IEncryptionProvider>(services);
		TryStub<Excalibur.Compliance.IEncryptionProviderRegistry>(services);
		TryStub<Excalibur.A3.Authentication.IAuthenticationToken>(services);
		TryStub<Excalibur.Dispatch.IEventSerializer>(services);
		TryStub<Excalibur.Compliance.IAuditStore>(services);
		TryStub<Excalibur.Compliance.ICascadeRelationshipResolver>(services);
		TryStub<Excalibur.Compliance.IKeyManagementProvider>(services);
		TryStub<Excalibur.EventSourcing.Queries.IGlobalStreamQuery>(services);
		TryStub<Excalibur.A3.Audit.IAuditMessagePublisher>(services);
		TryStub<Excalibur.Data.ElasticSearch.Security.IElasticsearchKeyProvider>(services);
		TryStub<Elastic.Clients.Elasticsearch.ElasticsearchClient>(services);
	}

	/// <summary>
	/// Registers <typeparamref name="T"/> as a service the container knows about but can never produce.
	/// </summary>
	/// <remarks>
	/// A factory descriptor is enough for container validation to construct the package's own types, and it
	/// is deliberately not a working implementation: nothing in these tests resolves one, and anything that
	/// did would get an exception rather than a silent substitute for infrastructure the consumer owns.
	/// </remarks>
	private static void Stub<T>(IServiceCollection services)
		where T : class
		=> services.AddSingleton<T>(StubFactory<T>());

	/// <summary>
	/// Registers <typeparamref name="T"/> as an unproducible service only if nothing already did.
	/// </summary>
	private static void TryStub<T>(IServiceCollection services)
		where T : class
		=> services.TryAddSingleton<T>(StubFactory<T>());

	private static Func<IServiceProvider, T> StubFactory<T>()
		where T : class
		=> static _ => throw new NotSupportedException(
			$"'{typeof(T).FullName}' is a consumer-supplied seam stubbed by the package DI smoke harness. " +
			"It exists so a package's own registrations can be validated and must never be resolved.");

	/// <summary>
	/// The minimal <see cref="IHostApplicationLifetime"/> a real host would supply.
	/// </summary>
	/// <remarks>
	/// A hosted service that takes a dependency on the host's shutdown signal is not a defect -- Quartz's
	/// own <c>QuartzHostedService</c> does it, and so does anything that must drain on shutdown. The
	/// tokens here are never signalled because nothing in these tests starts a host; the type exists so a
	/// package's own descriptors can be validated against the composition a real host would give them.
	/// </remarks>
	private sealed class SmokeHostApplicationLifetime : IHostApplicationLifetime
	{
		public CancellationToken ApplicationStarted => CancellationToken.None;

		public CancellationToken ApplicationStopping => CancellationToken.None;

		public CancellationToken ApplicationStopped => CancellationToken.None;

		public void StopApplication()
		{
			// Nothing in these tests starts a host, so there is nothing to stop.
		}
	}

	/// <summary>
	/// The minimal <see cref="IHostEnvironment"/> a real host would supply.
	/// </summary>
	private sealed class SmokeHostEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Development;
		public string ApplicationName { get; set; } = "Excalibur.Dispatch.Tests.Smoke";
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public IFileProvider ContentRootFileProvider { get; set; } =
			new PhysicalFileProvider(AppContext.BaseDirectory);
	}

	// ───────────────────────────────────────────────────────────────────
	// MemberData: all shipping packages with DI registration methods
	// ───────────────────────────────────────────────────────────────────

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

		// ══════════════════════════════════════════════════════════
		// DISPATCH RESILIENCE
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Dispatch.Resilience.Polly", s => s.AddPollyResilience());

		// ══════════════════════════════════════════════════════════
		// DISPATCH SECURITY
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Security", s =>
			s.AddDispatchSecurityMiddleware(
				(Excalibur.Security.SecurityOptions opt) => { }));
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
		yield return Reg("Excalibur.Compliance.Pdf", s => s.AddSoc2PdfExport());
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
		yield return Reg("Excalibur.Dispatch.Transport.Kafka [ConfluentFormat]", s =>
		{
			s.AddDispatch();
			s.AddKafkaTransport("kafka-smoke", kafka => kafka.BootstrapServers("localhost:9092"));
			s.AddConfluentFormat(static registry => registry.Url = "http://localhost:8081");
		});
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
		yield return Reg("Excalibur.Data.ElasticSearch [AzureKeyVaultCredentialStorage]",
			s => s.AddAzureKeyVaultCredentialStorage(new ConfigurationBuilder().Build()));
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
		// Excalibur.Outbox.InMemory has two public entry points that each register the store, and only
		// AddInMemoryOutboxStore() was graded. This is the other one -- the provider-selection seam the
		// package's own example uses -- so a registration defect present in one and not the other is
		// visible here rather than at a consumer's first resolve.
		yield return Reg("Excalibur.Outbox.InMemory [Builder]", s =>
			s.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseInMemory())));
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
		yield return Reg("Excalibur.Saga [Orchestration]", s => s.AddExcaliburOrchestration());
		yield return Reg("Excalibur.Saga.SqlServer", s =>
			s.AddSqlServerSagaStore(sql => { sql.ConnectionString = MockConnectionString; }));

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR A3 (Authentication, Authorization, Auditing)
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.A3", s => s.AddExcaliburA3());
		// AddA3DispatchServices and AddExcaliburAuthorization are composition steps of AddExcaliburA3
		// (A3ServiceCollectionExtensions.cs calls both), not standalone entry points: they register handlers
		// and middleware against the grant repository, policy provider and access token that the A3 core
		// registers. Compose them the way the package does.
		yield return Reg("Excalibur.A3 [DispatchServices]", s =>
		{
			_ = s.AddExcaliburA3();
			s.AddA3DispatchServices();
		});
		yield return Reg("Excalibur.A3 [Authorization]", s =>
		{
			_ = s.AddExcaliburA3();
			s.AddExcaliburAuthorization();
		});

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR APPLICATION
		// ══════════════════════════════════════════════════════════

		// S804 bd-sdhocq A7: AddExcaliburApplicationServices deleted. Use AddExcalibur + ScanAssemblies.
		yield return Reg("Excalibur.Application", s => s.AddExcalibur(b => b.ScanAssemblies(typeof(PackageDiSmokeTests).Assembly)));
		yield return Reg("Excalibur.Application [Activities]", s => s.AddActivities());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR CACHING & CDC
		// ══════════════════════════════════════════════════════════

		// AddExcaliburProjectionCaching is internal and documented as running after AddDispatchCaching,
		// which supplies the ICacheInvalidationService it decorates
		// (Excalibur.Dispatch.Caching/CachingServiceCollectionExtensions.cs).
		yield return Reg("Excalibur.Caching [Projections]", s =>
		{
			s.AddDispatchCaching();
			s.AddExcaliburProjectionCaching();
		});
		yield return Reg("Excalibur.Cdc", s => s.AddCdcProcessor());

		// ══════════════════════════════════════════════════════════
		// EXCALIBUR COMPLIANCE
		// ══════════════════════════════════════════════════════════

		yield return Reg("Excalibur.Compliance.MongoDb", s =>
			s.AddMongoDbComplianceStore(opts => opts.ConnectionString = "mongodb://localhost:27017"));
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
		yield return Reg("Excalibur.Hosting.HealthChecks [Memory]", s => _ = s.AddHealthChecks().AddMemoryHealthChecks());
		yield return Reg("Excalibur.Hosting.Jobs", s => s.AddExcalibur(x => x.AddJobs()));
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

		// ══════════════════════════════════════════════════════════
		// SECONDARY FEATURE ENTRY POINTS
		// ══════════════════════════════════════════════════════════
		// Every case above is a package's PRIMARY Add*(). These are the feature-level switches a
		// consumer calls to turn one thing on, and until now none of them was named by any test,
		// sample, benchmark or doc -- so they sat outside the only gate that catches a registration
		// whose dependency nothing supplies. A defect here passes every unit test and fails in the
		// consumer's host at start-up, which is exactly the shape this suite exists to catch.

		yield return Reg("Excalibur.Caching [AdaptiveTtl]", s =>
		{
			// AddAdaptiveTtlCache decorates a base IDistributedCache and fails fast without one, so the
			// base cache is a consumer choice the case must make -- not a dependency the package owes.
			s.AddDistributedMemoryCache();
			s.AddAdaptiveTtlCache();
		});
		yield return Reg("Excalibur.Data.Abstractions [CdcHealthCheckOptionsValidation]", s =>
			s.AddCdcHealthCheckOptionsValidation());
		yield return Reg("Excalibur.Compliance [ErasureSchemaValidation]", s => s.AddErasureSchemaValidation());
		yield return Reg("Excalibur.Compliance.Postgres [ComplianceBuilder]", s => s.AddPostgresCompliance(_ => { }));
		yield return Reg("Excalibur.Data.CosmosDb [Authorization]", s =>
			s.AddCosmosDbAuthorization(_ => { }));
		yield return Reg("Excalibur.Data.CosmosDb [GrantStore]", s => s.AddCosmosDbGrantStore(_ => { }));
		yield return Reg("Excalibur.Data.CosmosDb [ActivityGroupGrantStore]", s =>
			s.AddCosmosDbActivityGroupGrantStore(_ => { }));
		yield return Reg("Excalibur.Data.MongoDB [Authorization]", s => s.AddMongoDbAuthorization(_ => { }));
		yield return Reg("Excalibur.Data.MongoDB [GrantStore]", s => s.AddMongoDbGrantStore(_ => { }));
		yield return Reg("Excalibur.Data.MongoDB [ActivityGroupGrantStore]", s =>
			s.AddMongoDbActivityGroupGrantStore(_ => { }));
		yield return Reg("Excalibur.Data.Firestore [Authorization]", s => s.AddFirestoreAuthorization(_ => { }));
		yield return Reg("Excalibur.Data.SqlServer [SqlHealthCheck]", s =>
			s.AddHealthChecks().AddSqlHealthCheck(MockConnectionString, "sqlserver-smoke", TimeSpan.FromSeconds(5)));
		yield return Reg("Excalibur.LeaderElection.SqlServer [HealthCheck]", s =>
			s.AddHealthChecks().AddSqlServerLeaderElectionHealthCheck());
		yield return Reg("Excalibur.Outbox [StoreHealthCheck]", s =>
			s.AddHealthChecks().AddOutboxStoreHealthCheck());
		yield return Reg("Excalibur.Dispatch [Diagnostics]", s => s.AddDispatch().UseDiagnostics());
		yield return Reg("Excalibur.Dispatch [ThreadingBuilder]", s => s.AddDispatch().UseThreading());
		yield return Reg("Excalibur.Dispatch [ThreadingOptions]", s =>
			s.AddDispatch().UseThreading().WithThreadingOptions(_ => { }));
		yield return Reg("Excalibur.Dispatch.Transport.AwsSqs [SqsHealthCheck]", s =>
			s.AddHealthChecks().AddAwsSqsHealthCheck());
		yield return Reg("Excalibur.Dispatch.Transport.AwsSqs [SnsHealthCheck]", s =>
			s.AddHealthChecks().AddAwsSnsHealthCheck());
		yield return Reg("Excalibur.Dispatch.Transport.AzureServiceBus [EventGrid]", s =>
			s.AddEventGridTransport(eg =>
			{
				eg.TopicEndpoint = "https://smoke.eventgrid.azure.net/api/events";
				eg.AccessKey = "smoke-key";
			}));
		yield return Reg("Excalibur.Dispatch.Transport.GooglePubSub [OrderingKey]", s =>
			s.AddGooglePubSubOrderingKey());
		yield return Reg("Excalibur.Dispatch.Transport.Kafka [Admin]", s =>
			s.AddKafkaAdmin(admin => admin.BootstrapServers = "localhost:9092"));

		// ══════════════════════════════════════════════════════════
		// ENTRY POINTS THAT NEED A CONSUMER-SUPPLIED TYPE ARGUMENT
		// ══════════════════════════════════════════════════════════
		// These take the consumer's implementation as a TYPE ARGUMENT rather than resolving one, so a
		// stub factory cannot stand in for it -- without a concrete type the call does not compile. That
		// is why they sat outside this gate; the doubles in SmokeConsumerDoubles.cs are what closes it.

		yield return Reg("Excalibur.Dispatch.Transport.Abstractions [CloudEventEncoder]", s =>
			s.AddCloudEventEncoder<SmokeTransportMessage, SmokeCloudEventEncoder>());
		yield return Reg("Excalibur.Dispatch.Transport.Abstractions [CloudEventEncoderFactory]", s =>
			s.AddCloudEventEncoder<SmokeTransportMessage>(static _ => new SmokeCloudEventEncoder()));
		yield return Reg("Excalibur.Dispatch.Transport.GooglePubSub [SchemaManager]", s =>
			s.AddGooglePubSubSchemaManager<SmokePubSubSchemaManager>());
		yield return Reg("Excalibur.Dispatch.Transport.GooglePubSub [SchemaManagerFactory]", s =>
			s.AddGooglePubSubSchemaManager(static _ => new SmokePubSubSchemaManager()));
		yield return Reg("Excalibur.Dispatch.Transport.AzureServiceBus [Transactions]", s =>
			s.AddAzureServiceBusTransactions<SmokeAzureServiceBusTransaction>());

		// ══════════════════════════════════════════════════════════
		// PROJECTION STORES (per-projection generic registration)
		// ══════════════════════════════════════════════════════════
		// Each store's constructor takes the provider's own client, which is a consumer choice the case
		// must make -- registered here rather than in AddConsumerSuppliedSeams so it is scoped to these
		// two cases and cannot silently satisfy a dependency some other package owes.

		yield return Reg("Excalibur.Data.DynamoDb [ProjectionStore]", s =>
		{
			Stub<Amazon.DynamoDBv2.IAmazonDynamoDB>(s);
			s.AddDynamoDbProjectionStore<SmokeProjection>(o => o.TableName = "smoke-projections");
		});
		yield return Reg("Excalibur.Data.Firestore [ProjectionStore]", s =>
		{
			Stub<Google.Cloud.Firestore.FirestoreDb>(s);
			s.AddFirestoreProjectionStore<SmokeProjection>(o => o.CollectionName = "smoke-projections");
		});

		// ══════════════════════════════════════════════════════════
		// BUILDER-SCOPED ENTRY POINTS
		// ══════════════════════════════════════════════════════════
		// These hang off IExcaliburBuilder / IDispatchBuilder rather than IServiceCollection, so they are
		// only reachable through the composition the builder's own Add*() opens.

		yield return Reg("Excalibur.Hosting [BuilderContext]", s =>
			s.AddExcalibur(b => b.UseTenant("smoke-tenant").UseLocalClientAddress()));
		yield return Reg("Excalibur.Caching [ProjectionResolvers]", s =>
			s.AddDispatch().WithProjectionResolvers(typeof(SmokeProjectionTagResolver)));
		yield return Reg("Excalibur.Caching [ProjectionResolversFromAssembly]", s =>
			s.AddDispatch().WithProjectionResolversFromAssembly(typeof(PackageDiSmokeTests).Assembly));

		// ══════════════════════════════════════════════════════════
		// PACKAGES THE SMOKE PROJECT DID NOT REFERENCE
		// ══════════════════════════════════════════════════════════
		// Both ship as NuGet packages and both register a hosted/enumerable service, which is exactly
		// the shape that fails at host start rather than at registration. The missing project reference
		// was the only thing keeping them out; it is now present.

		yield return Reg("Excalibur.Inbox [SchemaValidation]", s => s.AddInboxSchemaValidation());
		yield return Reg("Excalibur.Operations.Dashboard [Throughput]", s => s.AddThroughputDashboard());

		// ══════════════════════════════
		// METAPACKAGES
		// ══════════════════════════════
		// A metapackage's whole value is that one call composes several packages, so it is the
		// composition most likely to register a component whose dependency another package was
		// supposed to supply -- and the one a consumer is most likely to call. Each case is the
		// single call the package's own XML doc gives as its example.

		yield return Reg("Excalibur.Dispatch.AspNetCore [Metapackage]", s => s.AddDispatchAspNetCore());
		yield return Reg("Excalibur.Dispatch.Aws [Metapackage]", s =>
			s.AddDispatchAws(aws => aws.UseRegion("us-east-1")));
		yield return Reg("Excalibur.Dispatch.Azure [Metapackage]", s =>
			s.AddDispatchAzure(asb =>
				asb.ConnectionString("Endpoint=sb://smoke.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test")));
		yield return Reg("Excalibur.Dispatch.Kafka [Metapackage]", s =>
			s.AddDispatchKafka(kafka => kafka.BootstrapServers("localhost:9092")));
		yield return Reg("Excalibur.Dispatch.RabbitMQ [Metapackage]", s =>
			s.AddDispatchRabbitMQ(rmq => rmq.HostName("localhost")));
		yield return Reg("Excalibur.Dispatch.SqlServer [Metapackage]", s =>
			s.AddDispatchWithSqlServer(MockConnectionString));
		yield return Reg("Excalibur.Dispatch.Postgres [Metapackage]", s =>
			s.AddDispatchWithPostgres(MockPostgresConnectionString));
		yield return Reg("Excalibur.SqlServer [Metapackage]", s =>
			s.AddExcaliburSqlServer(sql => sql.ConnectionString = MockConnectionString));
		yield return Reg("Excalibur.Postgres [Metapackage]", s =>
			s.AddExcaliburPostgres(pg => pg.ConnectionString = MockPostgresConnectionString));
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
