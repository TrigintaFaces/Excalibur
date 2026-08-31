// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Binds handler registration at every entry point that composes dispatch on a consumer's behalf.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddDispatch(Action&lt;IDispatchBuilder&gt;)</c> used to discover handlers from the entry assembly
/// whenever its lambda named none. The scan is gone: one branch in that body made the trim analyser treat
/// every caller as reflective, including callers that reflect over nothing. Every entry point that reaches
/// the overload now has to name the discovery itself, and this is the test that says whether it does.
/// </para>
/// <para>
/// Both arms are required. The safety arm alone would pass again the moment anyone quietly restored the
/// implicit fallback upstream — which is the change that has to stay reverted. The liveness arm pins the
/// other half of the contract: a consumer who supplies a configuration owns handler registration, and gets
/// nothing they did not name.
/// </para>
/// <para>
/// This project is the only one referencing all seven dispatch metapackages, the ASP.NET Core hosting
/// package, the Excalibur host builder and the test harness at once, so it is the only place the whole
/// population can be asserted in one theory rather than split across projects that each see part of it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HandlerDiscoveryAtEveryEntryPointShould
{
	private const string SqlServerConnectionString = "Server=smoke-test;Database=smoke;Trusted_Connection=true";

	private const string PostgresConnectionString = "Host=smoke-test;Database=smoke;Username=smoke;Password=smoke";

	private const string AzureConnectionString =
		"Endpoint=sb://smoke.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";

	/// <summary>
	/// Composes one entry point into a service collection, optionally with a consumer configuration.
	/// </summary>
	/// <remarks>
	/// Passing <see langword="null" /> and omitting the argument are the same call: the parameter's default
	/// value is null at every one of these sites.
	/// </remarks>
	private static readonly Dictionary<string, Action<IServiceCollection, Action<IDispatchBuilder>?>> Composers =
		new(StringComparer.Ordinal)
		{
			["services.AddDispatch()"] = static (services, configure) =>
				_ = configure is null ? services.AddDispatch() : services.AddDispatch(configure),

			["services.AddDispatchAspNetCore()"] = static (services, configure) =>
				_ = services.AddDispatchAspNetCore(configure),

			["services.AddDispatchAws()"] = static (services, configure) =>
				_ = services.AddDispatchAws(static aws => aws.UseRegion("us-east-1"), configure),

			["services.AddDispatchAzure()"] = static (services, configure) =>
				_ = services.AddDispatchAzure(static asb => asb.ConnectionString(AzureConnectionString), configure),

			["services.AddDispatchKafka()"] = static (services, configure) =>
				_ = services.AddDispatchKafka(static kafka => kafka.BootstrapServers("localhost:9092"), configure),

			["services.AddDispatchRabbitMQ()"] = static (services, configure) =>
				_ = services.AddDispatchRabbitMQ(static rmq => rmq.HostName("localhost"), configure),

			["services.AddDispatchWithSqlServer(connectionString)"] = static (services, configure) =>
				_ = services.AddDispatchWithSqlServer(SqlServerConnectionString, configure),

			["services.AddDispatchWithPostgres(connectionString)"] = static (services, configure) =>
				_ = services.AddDispatchWithPostgres(PostgresConnectionString, configure),

			["services.AddExcalibur(x => x.AddDispatch())"] = static (services, configure) =>
				_ = services.AddExcalibur(excalibur => excalibur.AddDispatch(configure)),
		};

	/// <summary>
	/// The two entry points that are not shaped as a service-collection extension.
	/// </summary>
	private static readonly Dictionary<string, Func<Action<IDispatchBuilder>?, ILoggerProvider, IServiceProvider>>
		HostShapedEntryPoints = new(StringComparer.Ordinal)
		{
			["webApplicationBuilder.AddDispatch()"] = static (configure, logs) =>
			{
				var builder = WebApplication.CreateBuilder();
				_ = builder.Services.AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
				_ = builder.AddDispatch(configure);
				return builder.Services.BuildServiceProvider();
			},

			["new DispatchTestHarness()"] = static (configure, logs) =>
			{
				var harness = new DispatchTestHarness();

				if (configure is not null)
				{
					_ = harness.ConfigureDispatch(configure);
				}

				// The harness pins ILogger<> to NullLogger<> before the consumer's service configuration
				// runs, so the diagnostic would be swallowed rather than absent. Displace it; the consumer
				// hook runs after AddDispatch, which is exactly why it can.
				_ = harness.ConfigureServices(services =>
				{
					services.RemoveAll(typeof(ILogger<>));
					services.RemoveAll<ILoggerFactory>();
					_ = services.AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
				});

				return harness.Services;
			},
		};

	public static TheoryData<string> AllEntryPoints
	{
		get
		{
			var data = new TheoryData<string>();

			foreach (var name in Composers.Keys)
			{
				data.Add(name);
			}

			foreach (var name in HostShapedEntryPoints.Keys)
			{
				data.Add(name);
			}

			return data;
		}
	}

	/// <summary>
	/// The five entry points that synthesise a dispatch configuration of their own, each paired with an
	/// assembly whose registrations prove the synthesised part still ran.
	/// </summary>
	/// <remarks>
	/// These are the sites where the consumer's own nullness is the only discriminator available: the
	/// metapackage always passes a non-null lambda down, so the callee cannot tell an unconfigured consumer
	/// from a configured one. The named assembly is the arm that catches the tempting wrong fix — routing
	/// the null case to the assembly overload restores handler discovery and silently drops the transport,
	/// resilience and observability the metapackage exists to add.
	/// </remarks>
	public static TheoryData<string, string> SynthesisingEntryPoints => new()
	{
		{ "services.AddDispatchAspNetCore()", "Excalibur.Dispatch.Observability" },
		{ "services.AddDispatchAws()", "Excalibur.Dispatch.Transport.AwsSqs" },
		{ "services.AddDispatchAzure()", "Excalibur.Dispatch.Transport.AzureServiceBus" },
		{ "services.AddDispatchKafka()", "Excalibur.Dispatch.Transport.Kafka" },
		{ "services.AddDispatchRabbitMQ()", "Excalibur.Dispatch.Transport.RabbitMQ" },
	};

	// ---------- AC-3 SAFETY: no argument means the consumer's handlers are registered ----------

	[Theory]
	[MemberData(nameof(AllEntryPoints))]
	public void Register_the_entry_assembly_handlers_when_called_with_no_optional_argument(string entryPoint)
	{
		// The scan resolves Assembly.GetEntryAssembly(). Under this runner that must be this test assembly,
		// which declares the probe handler — asserted rather than assumed, because if the entry assembly
		// were the host instead, every verdict below would be about a different assembly than it claims.
		var entryAssembly = Assembly.GetEntryAssembly();
		entryAssembly.ShouldNotBeNull("there is no entry assembly to discover handlers from");
		entryAssembly.GetTypes().ShouldContain(
			typeof(EntryPointProbeHandler),
			"the entry assembly must be the one declaring the probe handler, or this theory measures a "
			+ $"different assembly than it names (entry assembly was '{entryAssembly.GetName().Name}')");

		using var logs = new ListLoggerProvider();
		var provider = Compose(entryPoint, null, logs);

		provider.GetRequiredService<IHandlerRegistry>().GetAll().ShouldNotBeEmpty(
			$"'{entryPoint}' is documented as a one-line composition and the consumer named no handler, so "
			+ "it must discover them; registering none makes every published event vanish silently");

		DisposeProvider(provider);
	}

	// ---------- AC-3 LIVENESS: a supplied configuration owns handler registration ----------

	// The pinned-leak set that used to live here is gone, and so is its one member. It held
	// AddDispatchWithSqlServer, which composes the outbox, whose core registration called the
	// zero-argument AddDispatch() -- an unconditional entry-assembly scan, taken BEFORE the guard that
	// would have noticed the consumer had already configured handlers themselves. The outbox now
	// bootstraps through AddDispatchPipeline(), which registers the primitives and scans nothing, so the
	// entry point meets the strict assertions below like every other one. Nothing is exempt any more, and
	// a new leak has nowhere to be parked.

	[Theory]
	[MemberData(nameof(AllEntryPoints))]
	public async Task Register_no_handler_and_warn_when_a_handler_less_configuration_is_supplied(string entryPoint)
	{
		using var logs = new ListLoggerProvider();
		var provider = Compose(entryPoint, static _ => { }, logs);

		var registered = provider.GetRequiredService<IHandlerRegistry>().GetAll();

		registered.ShouldBeEmpty(
			$"'{entryPoint}' was given a configuration that names no handler, so it must register none. A "
			+ "non-empty registry here means an implicit entry-assembly scan is back somewhere upstream, "
			+ "which is the change that made the trim analyser condemn every caller");

		await StartTheHandlerDiagnosticAsync(provider);

		logs.Entries.ShouldContain(
			entry => entry.Level == LogLevel.Warning
				&& entry.Message.Contains("No message handlers are registered", StringComparison.Ordinal),
			$"'{entryPoint}' composed a pipeline with no handlers at all; silence there lets a host run "
			+ "indefinitely discarding every event it publishes");

		DisposeProvider(provider);
	}

	// ---------- AC-3b: the synthesised configuration must survive the no-argument call ----------

	[Theory]
	[MemberData(nameof(SynthesisingEntryPoints))]
	public void Keep_the_synthesised_configuration_when_called_with_no_optional_argument(
		string entryPoint,
		string requiredAssemblyName)
	{
		var noArgument = new ServiceCollection();
		Composers[entryPoint](noArgument, null);

		var supplied = new ServiceCollection();
		Composers[entryPoint](supplied, static _ => { });

		var noArgumentAssemblies = RegisteredAssemblyNames(noArgument);
		var suppliedAssemblies = RegisteredAssemblyNames(supplied);

		// Non-vacuity for the comparison below: the metapackage really does compose this package, so the
		// assertion that follows is about something the naive fix would actually lose.
		suppliedAssemblies.ShouldContain(
			requiredAssemblyName,
			$"'{entryPoint}' is supposed to compose '{requiredAssemblyName}' regardless of what the "
			+ "consumer configures; if it does not, this arm cannot detect the regression it exists for");

		noArgumentAssemblies.ShouldContain(
			requiredAssemblyName,
			$"'{entryPoint}' called with no argument dropped '{requiredAssemblyName}'. Routing the null "
			+ "case to the assembly overload restores handler discovery and discards the transport, "
			+ "resilience and observability this metapackage exists to add — register the handlers from "
			+ "inside the synthesised configuration instead");

		suppliedAssemblies.Except(noArgumentAssemblies, StringComparer.Ordinal).ShouldBeEmpty(
			$"'{entryPoint}' called with no argument registers less than the same call with an empty "
			+ "configuration. The no-argument path must add handler discovery, never remove anything");

		using var logs = new ListLoggerProvider();
		var provider = Compose(entryPoint, null, logs);

		provider.GetRequiredService<IHandlerRegistry>().GetAll().ShouldNotBeEmpty(
			$"'{entryPoint}' must register the consumer's handlers as well as the packages it composes");

		DisposeProvider(provider);
	}

	// ---------- AC-3c: do the two routes actually converge? ----------

	/// <summary>
	/// Records that the two routes into dispatch registration do NOT converge, and how they differ.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A bare <c>AddDispatch()</c> and <c>AddDispatch(configure)</c> were assumed to be interchangeable
	/// when the configuration only discovers handlers. Measured, they are not: they reach different
	/// overloads, and only the configure overload materialises the pipeline through the builder. That one
	/// call is the whole difference, and it is a behavioural fork rather than a formatting artefact.
	/// </para>
	/// <para>
	/// The consumer-visible half is the handler LIFETIME. Both routes register the discovered handlers
	/// transient; the builder route then runs the stateless-handler promotion, so the same handler ends up
	/// singleton through one entry point and transient through the other. The builder route additionally
	/// registers the options validators and the start-up diagnostics that the bare route never sees.
	/// </para>
	/// <para>
	/// This is pinned rather than asserted-away because nobody has ruled on which route is correct. If a
	/// ruling lands, this test goes RED and says so, which is the point of writing it down.
	/// </para>
	/// </remarks>
	[Fact]
	public void Diverge_between_the_bare_call_and_a_null_coalesced_configuration()
	{
		var bare = new ServiceCollection();
		_ = bare.AddDispatch();

		var coalesced = new ServiceCollection();
		Action<IDispatchBuilder>? absent = null;
		_ = coalesced.AddDispatch(absent ?? (static d => d.AddHandlersFromEntryAssembly()));

		DescribeAll(bare).ShouldNotBeEmpty("an empty baseline would make every comparison below vacuous");

		// Both routes must actually register the probe handler, or the lifetime comparison compares
		// nothing. This is the part that is NOT in question and must never regress.
		var bareProbe = bare.Single(IsProbeHandlerRegistration);
		var coalescedProbe = coalesced.Single(IsProbeHandlerRegistration);

		bareProbe.Lifetime.ShouldBe(
			ServiceLifetime.Transient,
			"the assembly overload registers discovered handlers transient and materialises no pipeline, "
			+ "so nothing promotes them");

		coalescedProbe.Lifetime.ShouldBe(
			ServiceLifetime.Singleton,
			"the configure overload calls Build(), which promotes an eligible stateless transient handler "
			+ "to singleton — the promotion the transient registration exists to keep available. The same "
			+ "handler therefore has a different lifetime depending on which entry point composed it");

		// The builder route alone registers the start-up surface. Naming it here keeps the difference
		// enumerated rather than summarised, so a change to either route is visible.
		var builderOnly = DescribeAll(coalesced)
			.Except(DescribeAll(bare), StringComparer.Ordinal)
			.ToList();

		builderOnly.ShouldContain(
			description => description.Contains("DispatchBuilderSentinel", StringComparison.Ordinal),
			"only the configure overload marks the collection as builder-configured");

		builderOnly.ShouldContain(
			description => description.Contains("NoHandlersRegisteredStartupWarning", StringComparison.Ordinal),
			"only the configure overload registers the empty-composition diagnostic, so a consumer who "
			+ "reaches dispatch through the bare call never receives that warning");
	}

	private static bool IsProbeHandlerRegistration(ServiceDescriptor descriptor) =>
		descriptor.ImplementationType == typeof(EntryPointProbeHandler)
		&& descriptor.ServiceType != typeof(EntryPointProbeHandler);

	// ---------- helpers ----------

	private static IServiceProvider Compose(string entryPoint, Action<IDispatchBuilder>? configure, ILoggerProvider logs)
	{
		if (HostShapedEntryPoints.TryGetValue(entryPoint, out var hostShaped))
		{
			return hostShaped(configure, logs);
		}

		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
		Composers[entryPoint](services, configure);
		return services.BuildServiceProvider();
	}

	private static void DisposeProvider(IServiceProvider provider)
	{
		if (provider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Starts the start-up diagnostic under test, and only that.
	/// </summary>
	/// <remarks>
	/// Starting every hosted service in the composition would measure a different property than the one
	/// asserted. Three of these compositions correctly REFUSE to start here and are right to: the Kafka and
	/// RabbitMQ transports reject a plaintext connection to a local broker, and the event-sourcing
	/// registration fails fast when no event types have been registered. Those are the packages behaving
	/// as designed against absent infrastructure, and letting them decide whether the handler diagnostic
	/// fired would make this arm a test of the test environment.
	/// </remarks>
	private static async Task StartTheHandlerDiagnosticAsync(IServiceProvider provider)
	{
		var diagnostics = provider.GetServices<IHostedService>()
			.Where(static hosted => hosted.GetType().Name == "NoHandlersRegisteredStartupWarning")
			.ToList();

		diagnostics.ShouldNotBeEmpty(
			"the composition registered no handler diagnostic at all, so the assertion that it warned "
			+ "would pass or fail for the wrong reason");

		foreach (var hosted in diagnostics)
		{
			await hosted.StartAsync(TestContext.Current.CancellationToken);
		}
	}

	/// <summary>
	/// The distinct assemblies contributing a registration, by service type and by implementation type.
	/// </summary>
	/// <remarks>
	/// Both sides are read because a package commonly registers its own concrete type against an interface
	/// declared in an abstractions package; reading only the service type would report the abstraction's
	/// assembly and miss the package that actually composed it.
	/// </remarks>
	private static HashSet<string> RegisteredAssemblyNames(IServiceCollection services)
	{
		var names = new HashSet<string>(StringComparer.Ordinal);

		foreach (var descriptor in services)
		{
			_ = names.Add(descriptor.ServiceType.Assembly.GetName().Name ?? "?");

			if (descriptor.ImplementationType is { } implementation)
			{
				_ = names.Add(implementation.Assembly.GetName().Name ?? "?");
			}
		}

		return names;
	}

	private static List<string> DescribeAll(IServiceCollection services) =>
		services.Select(Describe).OrderBy(static text => text, StringComparer.Ordinal).ToList();

	private static string Describe(ServiceDescriptor descriptor) => string.Create(
		CultureInfo.InvariantCulture,
		$"{descriptor.Lifetime} {descriptor.ServiceType.FullName} -> {ImplementationOf(descriptor)}");

	private static string ImplementationOf(ServiceDescriptor descriptor) =>
		descriptor.ImplementationType?.FullName
		?? (descriptor.ImplementationInstance is not null ? "<instance>" : null)
		?? (descriptor.ImplementationFactory is not null ? "<factory>" : "<none>");

	// The probe the discovery has to find. Declared here, in the entry assembly, and asserted to be there.
	internal sealed record EntryPointProbeAction : IDispatchAction;

	internal sealed class EntryPointProbeHandler : IActionHandler<EntryPointProbeAction>
	{
		public Task HandleAsync(EntryPointProbeAction message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed record LogEntry(LogLevel Level, string Message);

	private sealed class ListLoggerProvider : ILoggerProvider
	{
		private readonly List<LogEntry> _entries = [];

		public IReadOnlyList<LogEntry> Entries
		{
			get
			{
				lock (_entries)
				{
					return _entries.ToList();
				}
			}
		}

		public ILogger CreateLogger(string categoryName) => new ListLogger(this);

		public void Dispose()
		{
		}

		private void Record(LogLevel level, string message)
		{
			lock (_entries)
			{
				_entries.Add(new LogEntry(level, message));
			}
		}

		private sealed class ListLogger(ListLoggerProvider owner) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter)
			{
				ArgumentNullException.ThrowIfNull(formatter);
				owner.Record(logLevel, formatter(state, exception));
			}
		}
	}
}
