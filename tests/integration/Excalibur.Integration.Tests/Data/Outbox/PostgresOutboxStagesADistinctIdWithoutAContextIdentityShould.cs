// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Two messages staged from a context carrying no identity must get two distinct outbox rows.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OutboundMessage" />'s constructor mints a unique <c>Id</c>. This store overwrote it with the
/// context's message id and <strong>fell back to the empty string</strong>, which is not an identity: two
/// messages staged from a context with no id both became <c>""</c>, and the second insert failed on the
/// primary key. SQL Server never overwrites the minted value, so the empty case was also a silent
/// divergence between two providers of one interface.
/// </para>
/// <para>
/// <strong>Reachable, not hypothetical.</strong> A context's identity is legitimately absent when the wire
/// deserializer assigns it from an attribute the producer did not write
/// (<c>MessageContextSerializer</c> reads it with a lookup that yields null on a missing key, and the
/// setter records "assigned" even for null). So a consumer whose upstream omits the id attribute, staging
/// two messages to this provider, hit a primary-key violation on the second.
/// </para>
/// <para>
/// <strong>What makes this red:</strong> restoring <c>outboundMessage.Id = context.MessageId ?? string.Empty</c>.
/// The first enqueue stages <c>""</c> and the second throws on the primary key, so the arm fails on the
/// second call rather than on an assertion — which is the defect, observed through the store.
/// </para>
/// <para>
/// Oracle carries the identical line and the identical fix. It has no container fixture here, so this arm
/// binds the Postgres half only; the Oracle half is bound by the shared source change and is stated as
/// unbound rather than implied.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxStagesADistinctIdWithoutAContextIdentityShould
	: IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxStagesADistinctIdWithoutAContextIdentityShould(
		PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task StageTwoMessagesWithoutCollidingWhenTheContextCarriesNoIdentity()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// MessageId assigned to null, which is what the wire deserializer does when the producer wrote no
		// id attribute. A real MessageContext is required: the setter marks the value assigned even for
		// null, and only then does the getter stop minting one.
		await store.EnqueueAsync(new ProbeEvent(), ContextWithNoIdentity(), CancellationToken.None)
			.ConfigureAwait(false);

		await Should.NotThrowAsync(
			() => store.EnqueueAsync(new ProbeEvent(), ContextWithNoIdentity(), CancellationToken.None).AsTask())
			.ConfigureAwait(false);

		var staged = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var ids = staged.Select(m => m.Id).ToList();

		ids.Count.ShouldBe(2, "both messages must stage; a shared empty id loses the second to the primary key.");
		ids.ShouldAllBe(id => !string.IsNullOrEmpty(id));
		ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
	}

	[Fact]
	public async Task StillStageUnderTheContextsOwnIdentityWhenItHasOne()
	{
		// Liveness. The arm above is satisfied by a store that ignores the context identity entirely, which
		// would discard the caller's chosen row id on every stage. When the context HAS an identity it is
		// still the one used.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var context = new MessageContext { MessageId = "zz-caller-supplied-row-id" };

		await store.EnqueueAsync(new ProbeEvent(), context, CancellationToken.None).ConfigureAwait(false);

		var staged = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		staged.Select(m => m.Id).ShouldContain("zz-caller-supplied-row-id");
	}

	private static MessageContext ContextWithNoIdentity() => new() { MessageId = null };

	private async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - this arm is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
		};

		return new PostgresOutboxStore(db, Options.Create(options), NullLogger<PostgresOutboxStore>.Instance);
	}

	private sealed record ProbeEvent : IIntegrationEvent;
}
