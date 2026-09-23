// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Inbox;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Locks what <c>UseInbox</c> means on the full-stack metapackages: the inbox is OPERATIVE, so a
/// redelivery of a message the host already processed does not reach the handler a second time.
/// </summary>
/// <remarks>
/// <para>
/// The defect: the option registered the inbox STORE and nothing else. Deduplication is performed by
/// the inbox middleware, which entered the pipeline only through an explicit <c>UseInbox()</c> call on
/// the dispatch builder that no metapackage made — so a consumer who took the default got a populated
/// store, no deduplication, and no signal that anything was missing. An absence of duplicate
/// suppression is indistinguishable from an absence of duplicates, so it could not be detected from
/// the host's own behaviour.
/// </para>
/// <para>
/// The arms substitute the in-memory inbox store for the provider one. The store is provider
/// infrastructure that needs a database; the subject here is the metapackage's WIRING, which does not.
/// Both metapackages are covered because both carry the option and both were unwired.
/// </para>
/// <para>
/// Paired deliberately. The deduplication arms assert a redelivery is suppressed (safety); the
/// opt-out arm asserts the handler still runs twice once the option is turned off (liveness).
/// The safety arms alone are satisfiable by a pipeline that drops every second message for an
/// unrelated reason.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class MetapackageInboxDeduplicationShould : UnitTestBase
{
	// Syntactically valid, never connected to: the in-memory store below serves every inbox read.
	private const string SqlServerConnectionString =
		"Server=localhost;Database=ExcaliburInboxWiringTests;Trusted_Connection=True;TrustServerCertificate=True";

	private const string PostgresConnectionString =
		"Host=localhost;Database=excalibur_inbox_wiring_tests;Username=postgres;Password=postgres";

	private const string TestPepper = "metapackage-registration-test-pepper-0123456789";

	private static readonly Assembly TestAssembly = typeof(MetapackageInboxDeduplicationShould).Assembly;

	[Fact]
	public async Task SuppressARedeliveryUnderTheSqlServerMetapackageDefaults()
	{
		var services = NewServices(out var handler);
		_ = services.AddExcaliburSqlServer(sql =>
		{
			sql.ConnectionString = SqlServerConnectionString;
			_ = sql.ConfigureDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));
		});

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			1,
			"AddExcaliburSqlServer leaves UseInbox at its default, and UseInbox means the inbox is " +
			"operative — a redelivered message must not reach the handler again.");
	}

	[Fact]
	public async Task SuppressARedeliveryUnderThePostgresMetapackageDefaults()
	{
		var services = NewServices(out var handler);
		_ = services.AddExcaliburPostgres(pg =>
		{
			pg.ConnectionString = PostgresConnectionString;
			_ = pg.ConfigureDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));
		});

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			1,
			"AddExcaliburPostgres leaves UseInbox at its default, and UseInbox means the inbox is " +
			"operative — a redelivered message must not reach the handler again.");
	}

	[Fact]
	public async Task StillDeliverTheRedeliveryWhenTheInboxIsTurnedOff()
	{
		// Liveness. Without this, a pipeline that swallowed every second message for some unrelated
		// reason would satisfy both arms above.
		var services = NewServices(out var handler);
		_ = services.AddExcaliburSqlServer(sql =>
		{
			sql.ConnectionString = SqlServerConnectionString;
			sql.UseInbox = false;
			_ = sql.ConfigureDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));
		});

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			2,
			"UseInbox = false opts out of deduplication, so both deliveries must reach the handler.");
	}

	[Fact]
	public async Task StillRunTheHandlerOnceWhenTheHostAlsoPlacesTheMiddlewareItself()
	{
		// A host that copied UseInbox() out of the granular docs and also took the metapackage default
		// places the middleware twice. Two passes are not merely wasteful: the second sees the message the
		// first admitted, reads it as a duplicate, and suppresses the handler entirely — so the count that
		// catches the regression is ZERO, not two.
		var services = NewServices(out var handler);
		_ = services.AddExcaliburSqlServer(sql =>
		{
			sql.ConnectionString = SqlServerConnectionString;
			_ = sql.ConfigureDispatch(dispatch =>
			{
				_ = dispatch.AddHandlersFromAssembly(TestAssembly);
				_ = dispatch.UseInbox();
			});
		});

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			1,
			"placing the inbox middleware twice must not suppress the first delivery as a duplicate of itself.");
	}

	[Fact]
	public async Task SuppressARedeliveryWhenTheHostPlacesTheMiddlewareThroughTheBuilderAlone()
	{
		// The documented one-call path, and the one the samples use: no metapackage, no WithInboxMode(),
		// just builder.UseInbox(). Calling it IS the statement that the inbox should be operative, so it
		// must deduplicate on its own — a host that placed the middleware exactly as the XML docs show
		// and got nothing is the same defect this bead exists to close, one layer in.
		var services = NewServices(out var handler);
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.UseInbox();
			_ = dispatch.AddHandlersFromAssembly(TestAssembly);
		});

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			1,
			"builder.UseInbox() is the documented way to place the inbox, so it must deduplicate " +
			"without the host also selecting a mode.");
	}

	[Fact]
	public async Task SuppressARedeliveryWhenTheInboxIsChainedOffTheAssemblyOverload()
	{
		// The same statement as the arm above, made on the builder AddDispatch(Assembly[]) returns instead of
		// inside a configuration callback. That builder used to accept UseInbox(), register the middleware
		// and its options, and leave it out of the pipeline -- so every redelivery reached the handler. The
		// arm above is the control: the same Use*() through the callback deduplicates.
		var services = NewServices(out var handler);
		_ = services.AddDispatch(TestAssembly).UseInbox();

		await DispatchTwiceAsync(services).ConfigureAwait(true);

		handler.Invocations.ShouldBe(
			1,
			"UseInbox() chained off AddDispatch(Assembly[]) places the inbox, so a redelivered message must " +
			"not reach the handler again.");
	}

	private static ServiceCollection NewServices(out RedeliveredActionHandler.Recorder recorder)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = TestPepper);

		recorder = new RedeliveredActionHandler.Recorder();
		_ = services.AddSingleton(recorder);

		return services;
	}

	private static async Task DispatchTwiceAsync(ServiceCollection services)
	{
		// Substitutes the provider store, which would open a real connection. Registered through the
		// in-memory package's own keyed entry and then bound last as the unkeyed IInboxStore, so it wins
		// the resolution the middleware performs. The wiring under test is unaffected by which store answers.
		_ = services.AddInMemoryInboxStore();
		_ = services.AddSingleton(static sp => sp.GetRequiredKeyedService<IInboxStore>("inmemory"));

		await using var provider = services.BuildServiceProvider();

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var contextFactory = provider.GetRequiredService<IMessageContextFactory>();

		// One message id, delivered twice — a redelivery, which is what an at-least-once transport
		// produces and what the inbox exists to absorb.
		var messageId = $"oxwyma-{Guid.NewGuid():N}";

		for (var delivery = 0; delivery < 2; delivery++)
		{
			var context = contextFactory.CreateContext();
			context.MessageId = messageId;

			var result = await dispatcher
				.DispatchAsync(new RedeliveredAction(), context, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);

			result.Succeeded.ShouldBeTrue(result.ErrorMessage ?? "dispatch failed");
		}
	}

	internal sealed class RedeliveredAction : IDispatchAction;

	internal sealed class RedeliveredActionHandler(RedeliveredActionHandler.Recorder recorder)
		: IActionHandler<RedeliveredAction>
	{
		public Task HandleAsync(RedeliveredAction action, CancellationToken cancellationToken)
		{
			recorder.Record();
			return Task.CompletedTask;
		}

		internal sealed class Recorder
		{
			private int _invocations;

			public int Invocations => Volatile.Read(ref _invocations);

			public void Record() => _ = Interlocked.Increment(ref _invocations);
		}
	}
}
