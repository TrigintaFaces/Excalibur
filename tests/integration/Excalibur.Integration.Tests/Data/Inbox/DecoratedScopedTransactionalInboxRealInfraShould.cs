// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Inbox.Diagnostics;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure atomicity lock for the inbox <b>decorators</b> over the scoped exactly-once seam
/// <see cref="IScopedTransactionalInboxStore"/>, exercised against a live MongoDB <b>replica set</b> — a real
/// provider whose native multi-document transaction a hand-written double cannot reproduce.
/// </summary>
/// <remarks>
/// <para>
/// The provider-level locks (<see cref="MongoDbTransactionalInboxExactlyOnceShould"/> and its siblings) prove
/// the bare store. This lock proves the <b>decorated combination</b>: a decorator that omitted the scoped seam
/// would make the exactly-once path invisible through decoration and silently downgrade the guarantee to the
/// at-least-once claim protocol, with no provider-level test able to see it.
/// </para>
/// <para>
/// SELECTION arm — the decorated store is actually chosen by <see cref="InboxMiddleware"/> on the
/// scoped-transactional path, proven by the handler observing a non-null <see cref="IInboxTransactionScope"/>
/// (the middleware exposes that on no other path), never by a type test the production code does not perform.
/// </para>
/// <para>
/// SAFETY arm — a handler that throws inside the scope leaves NEITHER the processed-mark NOR its enlisted
/// write behind, read back from a FRESH client. LIVENESS arm (mandatory) — on the success path the handler
/// runs, the mark commits, and both are observable from a FRESH client; a decorator that aborted every
/// transaction would satisfy safety alone and must not pass here.
/// </para>
/// <para>
/// NEVER SKIPPED — a missing container fails fast (<c>DockerAvailable.ShouldBeTrue</c>), so an absent
/// replica set surfaces as a failure rather than a silent pass. The MongoDB client is built by the store from
/// its own options with the driver's DEFAULT serializer — no hand-configured client stands between the lock
/// and the shipped wire shape.
/// </para>
/// <para>
/// NON-VACUOUS: against the pre-fix decorator shape (no <c>IScopedTransactionalInboxStore</c> in the
/// declaration) the middleware falls through to the weaker claim path so the handler observes a null scope,
/// and the direct casts throw <see cref="InvalidCastException"/> — every arm below is RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class DecoratedScopedTransactionalInboxRealInfraShould
	: IClassFixture<MongoDbTransactionalInboxReplicaSetFixture>, IAsyncLifetime
{
	private const string SideCollectionName = "inbox_decorated_txn_side_effect";
	private const string TelemetryDecorator = "telemetry";
	private const string EncryptingDecorator = "encrypting";

	private readonly MongoDbTransactionalInboxReplicaSetFixture _fixture;

	public DecoratedScopedTransactionalInboxRealInfraShould(MongoDbTransactionalInboxReplicaSetFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB replica-set container must be available - the real-infra decorated-inbox atomicity lock is never skipped.");
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync() => await _fixture.CleanupAsync().ConfigureAwait(false);

	// ----- SELECTION: the decorated store is chosen by the middleware on the scoped path -----

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public async Task Be_selected_by_the_middleware_on_the_scoped_transactional_path(string decorator)
	{
		var messageId = Guid.NewGuid().ToString();
		var middleware = CreateMiddleware(Decorate(decorator, CreateStore()));

		IInboxTransactionScope? observedScope = null;
		bool? observedInNativeTransaction = null;
		var result = await InvokeAsync(middleware, messageId, ctx =>
		{
			observedScope = ctx.GetInboxTransactionScope();

			// Read the live session WHILE the handler is inside the scope - after the middleware returns the
			// store has committed and disposed it, so this can only be observed here.
			observedInNativeTransaction = observedScope?.AsMongoSession().IsInTransaction;
			return true;
		}).ConfigureAwait(false);

		_ = observedScope.ShouldNotBeNull(
			"the middleware exposes a native transaction scope ONLY on the scoped-transactional path; a null scope means the "
			+ $"{decorator}-decorated MongoDB store was silently downgraded to the weaker at-least-once claim path");
		result.Succeeded.ShouldBeTrue("the handler's success result flows back through the scoped path");

		// The scope really is the provider's native session, not an adapter that merely satisfies the type.
		observedInNativeTransaction.ShouldBe(true,
			"the scope handed to the handler is the store's live MongoDB session, inside its native transaction");

		// LIVENESS through the middleware — the mark is durable on a FRESH store instance.
		(await CreateStore().IsProcessedAsync(messageId, MiddlewareHandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("a successfully handled message is durably marked processed through the decorated scoped path");
	}

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public async Task Leave_no_processed_mark_when_the_handler_fails_through_the_middleware(string decorator)
	{
		var messageId = Guid.NewGuid().ToString();
		var middleware = CreateMiddleware(Decorate(decorator, CreateStore()));

		var result = await InvokeAsync(middleware, messageId, _ => false).ConfigureAwait(false);

		result.Succeeded.ShouldBeFalse("a failing handler's result must surface, not be swallowed");
		(await CreateStore().IsProcessedAsync(messageId, MiddlewareHandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a failed handler rolls the native transaction back so the message is redelivered");
	}

	// ----- SAFETY: a throwing handler rolls back BOTH the mark and its enlisted write -----

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public async Task Roll_back_both_the_mark_and_the_enlisted_write_when_the_handler_throws(string decorator)
	{
		var sut = (IScopedTransactionalInboxStore)Decorate(decorator, CreateStore());
		var messageId = Guid.NewGuid().ToString();
		var handlerType = $"Handler.Type.{decorator}.RollbackThrow";
		var marker = Guid.NewGuid().ToString();

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				async (scope, ct) =>
				{
					await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
					throw new InvalidOperationException("simulated handler crash inside the native transaction");
				},
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// NEITHER survives — read back on a FRESH store instance / FRESH client.
		(await CreateStore().IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse($"a thrown handler must leave no processed-mark behind the {decorator} decorator");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(0, "the handler's enlisted write rolls back atomically with the processed-mark on throw");
	}

	// ----- LIVENESS: the success path commits BOTH, durably -----

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public async Task Commit_both_the_mark_and_the_enlisted_write_on_the_success_path(string decorator)
	{
		var sut = (IScopedTransactionalInboxStore)Decorate(decorator, CreateStore());
		var messageId = Guid.NewGuid().ToString();
		var handlerType = $"Handler.Type.{decorator}.Commit";
		var marker = Guid.NewGuid().ToString();

		var processed = await sut.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) => await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false),
			CancellationToken.None).ConfigureAwait(false);

		// A decorator that aborted every transaction would fail here — this is the mandatory liveness arm.
		processed.ShouldBeTrue($"a first successful transactional process returns true through the {decorator} decorator");
		(await CreateStore().IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the processed-mark commits and is durable on a fresh store instance");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "the handler's enlisted write commits atomically with the processed-mark");
	}

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public async Task Not_invoke_the_handler_for_an_already_processed_message(string decorator)
	{
		var sut = (IScopedTransactionalInboxStore)Decorate(decorator, CreateStore());
		var messageId = Guid.NewGuid().ToString();
		var handlerType = $"Handler.Type.{decorator}.Duplicate";
		var marker = Guid.NewGuid().ToString();
		var invocations = 0;

		var first = await sut.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) =>
			{
				_ = Interlocked.Increment(ref invocations);
				await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		var duplicate = await sut.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) =>
			{
				_ = Interlocked.Increment(ref invocations);
				await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		first.ShouldBeTrue("the first delivery processes the message");
		duplicate.ShouldBeFalse("an already-processed message is a duplicate - the handler is not invoked");
		invocations.ShouldBe(1, "the handler ran exactly once - never on the duplicate delivery");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "the committed handler effect happens exactly once - the duplicate never wrote");
	}

	// ----- capability reporting, over the REAL store rather than a double -----

	[Theory]
	[InlineData(TelemetryDecorator)]
	[InlineData(EncryptingDecorator)]
	public void Report_SupportsTransactional_over_a_real_document_store(string decorator)
	{
		var sut = (IInboxStoreCapabilities)Decorate(decorator, CreateStore());

		sut.SupportsTransactional.ShouldBeTrue(
			"MongoDbInboxStore implements ONLY IScopedTransactionalInboxStore; reporting false here makes the middleware's "
			+ "capability probe skip the exactly-once path for exactly the document stores this seam serves");
	}

	// ----- harness -----

	private static readonly string MiddlewareHandlerType = typeof(DecoratedScopedProbeMessage).FullName!;

	// The store builds its own MongoClient from its options - the driver's DEFAULT serializer, no hand-configured
	// client between the lock and the shipped wire shape.
	private MongoDbInboxStore CreateStore() => new(
		Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			EnableTransactions = true,
		}),
		NullLogger<MongoDbInboxStore>.Instance);

	// Typed as IInboxStore so the casts in the arms above are legal regardless of the decorator's declared
	// interface set - the pre-fix shape then fails at RUNTIME (a test RED), which is the non-vacuity signal.
	private static IInboxStore Decorate(string decorator, IInboxStore inner) => decorator switch
	{
		TelemetryDecorator => new TelemetryInboxStoreDecorator(inner),
		EncryptingDecorator => new EncryptingInboxStoreDecorator(
			inner,
			A.Fake<IEncryptionProviderRegistry>(),
			Options.Create(new EncryptionOptions { Mode = EncryptionMode.EncryptAndDecrypt, DefaultPurpose = "test" })),
		_ => throw new ArgumentOutOfRangeException(nameof(decorator), decorator, "unknown decorator"),
	};

	private static InboxMiddleware CreateMiddleware(IInboxStore store) => new(
		Options.Create(new InboxConfigurationOptions { Enabled = true }),
		store,
		deduplicator: null,
		new DispatchJsonSerializer(),
		NullLogger<InboxMiddleware>.Instance);

	private static async Task<IMessageResult> InvokeAsync(
		InboxMiddleware middleware,
		string messageId,
		Func<IMessageContext, bool> handler)
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns(messageId);

		DispatchRequestDelegate next = (_, ctx, _) =>
		{
			var succeeded = handler(ctx);
			var result = A.Fake<IMessageResult>();
			_ = A.CallTo(() => result.Succeeded).Returns(succeeded);
			return new ValueTask<IMessageResult>(result);
		};

		return await middleware.InvokeAsync(new DecoratedScopedProbeMessage(), context, next, CancellationToken.None)
			.ConfigureAwait(false);
	}

	// Writes a side-effect doc ENLISTED in the handler's session so it commits/aborts atomically with the mark.
	private async ValueTask WriteSideEffectInSessionAsync(IInboxTransactionScope scope, string marker, CancellationToken ct)
	{
		var session = scope.AsMongoSession();
		var collection = session.Client
			.GetDatabase(_fixture.DatabaseName)
			.GetCollection<BsonDocument>(SideCollectionName);
		await collection.InsertOneAsync(
			session,
			new BsonDocument { { "marker", marker } },
			cancellationToken: ct).ConfigureAwait(false);
	}

	// Counts committed side-effect docs on a FRESH client outside any session, so only durable writes are visible.
	private async Task<long> CountSideEffectsAsync(string marker)
	{
		var client = new MongoClient(_fixture.ConnectionString);
		return await client.GetDatabase(_fixture.DatabaseName)
			.GetCollection<BsonDocument>(SideCollectionName)
			.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("marker", marker))
			.ConfigureAwait(false);
	}
}

/// <summary>A message whose type name is the handler-type key the middleware derives.</summary>
internal sealed class DecoratedScopedProbeMessage : IDispatchMessage;
