// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

#pragma warning disable CA1812 // Instantiated by xUnit.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// qpvcu8 (S872, 02sj2h E2) — end-to-end exactly-once conformance for the turnkey
/// <c>AddExactlyOnceMessaging</c> composition (qzp9k7) under fault injection (duplicate submit + consumer
/// crash mid-flight + redelivery), against a live SQL Server container.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddExactlyOnceMessaging&lt;TOutboxStore, TInboxStore&gt;</c> is a COMPOSITION of the shipped
/// <c>AddOutbox</c> + <c>AddInbox</c> registrations plus the dedup window — not a new primitive. This lock
/// resolves the inbox THROUGH the composed registration (not a hand-constructed store) and proves the wired
/// component actually delivers exactly-once: a crashed consumer redelivers, and the committed handler effect
/// is applied exactly once across the duplicate + redelivery.
/// </para>
/// <para>
/// NEVER SKIPPED — Docker unavailability fails the fixture fast. NON-VACUOUS: RED against the pre-seam
/// baseline because <see cref="SqlServerInboxStore"/> does not yet implement
/// <see cref="ITransactionalInboxStore"/>, so the composition cannot yet resolve a transactional inbox
/// (the exactly-once guarantee boundary). Also RED against a composition that resolves a non-transactional
/// inbox, or an impl that double-applies the effect on redelivery.
/// </para>
/// </remarks>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class ExactlyOnceMessagingCompositionE2EShould : IAsyncLifetime
{
	/// <summary>
	/// Dedicated database holding the SINGLE-TENANT inbox schema for this composition.
	/// </summary>
	/// <remarks>
	/// The shared fixture's table is deliberately MULTI-TENANT (PK <c>(MessageId, HandlerType, TenantId)</c>
	/// with <c>TenantId NOT NULL</c>) because the tenant-isolation locks need that shape. This E2E composes
	/// the TURNKEY DEFAULT — <c>AddExactlyOnceMessaging</c> with no <c>AddMultiTenancy</c> — which resolves a
	/// single-tenant store, so <c>InboxSchemaContract.Verify</c> correctly fail-fasts against the shared
	/// table. Registering multi-tenancy to satisfy it would change WHAT IS UNDER TEST (the default
	/// composition IS the subject). Both shapes ship under the same table name, so they are separated by
	/// database rather than by renaming the table in a copied script.
	/// </remarks>
	private const string SingleTenantDatabaseName = "inbox_exactlyonce_e2e";

	private readonly SqlServerInboxStoreContainerFixture _fixture;
	private string _inboxConnectionString = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExactlyOnceMessagingCompositionE2EShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server inbox container fixture.</param>
	public ExactlyOnceMessagingCompositionE2EShould(SqlServerInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra E2E exactly-once lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		_inboxConnectionString = await ShippedInboxSchema.EnsureDatabaseAndSchemaAsync(
			_fixture.ConnectionString, SingleTenantDatabaseName, CancellationToken.None).ConfigureAwait(false);
		await ShippedInboxSchema.ResetAsync(_inboxConnectionString, CancellationToken.None).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_inboxConnectionString.Length > 0)
		{
			await ShippedInboxSchema.ResetAsync(_inboxConnectionString, CancellationToken.None).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task ExactlyOnceComposition_UnderDuplicateSubmitAndConsumerCrash_AppliesEffectExactlyOnce()
	{
		await using var provider = BuildComposedProvider();

		// Resolve the inbox THROUGH the composed registration (keyed "default", the seam AddInbox wires).
		var inbox = provider.GetRequiredKeyedService<IInboxStore>("default");

		// The composed registration must resolve a TRANSACTIONAL inbox to honor the exactly-once boundary —
		// RED until SqlServerInboxStore implements ITransactionalInboxStore.
		var transactional = inbox as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"AddExactlyOnceMessaging must compose a transactional inbox (ITransactionalInboxStore) so its " +
			"documented exactly-once guarantee boundary holds end-to-end.");

		// A single logical message submitted (and duplicated) into the pipeline.
		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.E2E";
		var committedEffects = 0;

		// Fault 1 — consumer crash mid-flight on first delivery: the transaction rolls back, nothing marked.
		// Tolerate either failure signal (rethrow or return) and assert the durable rolled-back outcome.
		try
		{
			_ = await transactional.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				(_, _) => throw new InvalidOperationException("simulated consumer crash mid-flight"),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected if the impl propagates the handler failure to the caller.
		}

		(await inbox.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a crashed consumer must leave the message unprocessed and redeliverable");

		// Redelivery — handler succeeds and its effect commits atomically with the processed-mark.
		var first = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				Interlocked.Increment(ref committedEffects);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);
		first.ShouldBeTrue("the redelivered message is processed successfully exactly once");

		// Fault 2 — DUPLICATE submit of the same message: the handler effect must NOT be re-applied.
		var duplicate = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				Interlocked.Increment(ref committedEffects);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);
		duplicate.ShouldBeFalse("a duplicate submit of an already-processed message is suppressed");

		// End-to-end exactly-once: exactly ONE committed handler effect despite crash + redelivery + duplicate.
		committedEffects.ShouldBe(1,
			"the handler effect must be applied exactly once end-to-end under duplicate submit and consumer crash");
	}

	private ServiceProvider BuildComposedProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Turnkey composition under test: AddOutbox + AddInbox + dedup behind one call.
		_ = services.AddExactlyOnceMessaging<SqlServerOutboxStore, SqlServerInboxStore>(options =>
			options.RequireTransactionalExactlyOnce = true);

		// Provider-specific store options the composed store constructors bind (the outbox side is part of
		// the composition; only the inbox is resolved by this E2E).
		_ = services.Configure<SqlServerInboxOptions>(options =>
		{
			// The dedicated single-tenant database; schema/table are the shipped script's own names.
			options.ConnectionString = _inboxConnectionString;
			options.SchemaName = "dbo";
			options.TableName = "inbox_messages";
		});
		_ = services.Configure<SqlServerOutboxOptions>(options =>
			options.ConnectionString = _fixture.ConnectionString);

		return services.BuildServiceProvider();
	}
}
