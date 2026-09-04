// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// A decorated event store must not hide a capability the store beneath it genuinely provides. The
/// transactional append is the one that matters most: when it is hidden, the repository silently leaves
/// the atomic append-and-stage path and nothing reports it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreCapabilityForwardingShould
{
	private const string Tenant = "tenant-a";

	[Fact]
	public void ExposeTheTransactionalCapabilityThroughTheTenantDecorator()
	{
		var inner = new TransactionalStore();

		IEventStore decorated = new TenantScopedEventStore(inner, new MutableTenantContext { TenantId = Tenant });

		decorated.GetService(typeof(ITransactionalEventStore))
			.ShouldNotBeNull("the decorated store is transactional, so the decorator must expose it");
	}

	[Fact]
	public void ExposeTheTransactionalCapabilityThroughAnObservationalDecorator()
	{
		// An observational decorator imposes no invariant, so it forwards every capability untouched.
		IEventStore decorated = new PassThroughObservationalStore(new TransactionalStore());

		decorated.GetService(typeof(ITransactionalEventStore)).ShouldNotBeNull();
	}

	[Fact]
	public void ExposeTheTransactionalCapabilityThroughNestedDecorators()
	{
		IEventStore decorated = new PassThroughObservationalStore(
			new TenantScopedEventStore(new TransactionalStore(), new MutableTenantContext { TenantId = Tenant }));

		decorated.GetService(typeof(ITransactionalEventStore)).ShouldNotBeNull();
	}

	[Fact]
	public async Task PerformTheTransactionalAppendThroughTheDecorator()
	{
		var inner = new TransactionalStore();
		IEventStore decorated = new TenantScopedEventStore(inner, new MutableTenantContext { TenantId = Tenant });

		var capability = (ITransactionalEventStore)decorated.GetService(typeof(ITransactionalEventStore))!;

		_ = await capability.AppendWithOutboxStagingAsync(
			"agg-1", "Agg", [], 0, static (_, _) => ValueTask.CompletedTask, TestContext.Current.CancellationToken);

		// The capability must actually work, not merely resolve.
		inner.TransactionalAppends.ShouldBe(1);
	}

	[Fact]
	public void NotAdvertiseTheTransactionalCapabilityOverAStoreThatLacksIt()
	{
		IEventStore decorated = new TenantScopedEventStore(
			new PlainStore(), new MutableTenantContext { TenantId = Tenant });

		decorated.GetService(typeof(ITransactionalEventStore)).ShouldBeNull();
	}

	[Fact]
	public async Task StillFailClosedOnTheTransactionalCapabilityWhenNoTenantIsAmbient()
	{
		var inner = new TransactionalStore();
		IEventStore decorated = new TenantScopedEventStore(inner, new MutableTenantContext { TenantId = null });

		var capability = (ITransactionalEventStore)decorated.GetService(typeof(ITransactionalEventStore))!;

		_ = await Should.ThrowAsync<TenantRequiredException>(async () =>
			await capability.AppendWithOutboxStagingAsync(
				"agg-1", "Agg", [], 0, static (_, _) => ValueTask.CompletedTask, TestContext.Current.CancellationToken));

		inner.TransactionalAppends.ShouldBe(0, "the unscoped append must never reach the store");
	}

	[Fact]
	public async Task RouteTheCapabilityViewsReadSurfaceBackThroughTheDecorator()
	{
		// The capability derives from IEventStore, so the view is itself an event store. It must not become
		// an unmediated handle on the store beneath the decorator.
		var inner = new TransactionalStore();
		IEventStore decorated = new TenantScopedEventStore(inner, new MutableTenantContext { TenantId = null });

		var view = (IEventStore)decorated.GetService(typeof(ITransactionalEventStore))!;

		_ = await Should.ThrowAsync<TenantRequiredException>(async () =>
			await view.LoadAsync("agg-1", "Agg", TestContext.Current.CancellationToken));
	}

	// -----------------------------------------------------------------------------------------------
	// TieredEventStoreDecorator. Reads span two tiers, because the archive service deletes from hot
	// once it has copied to cold.
	// -----------------------------------------------------------------------------------------------

	[Fact]
	public void ExposeTheTransactionalCapabilityThroughTheTieredDecorator()
	{
		// The acceptance criterion for tiered storage: enabling it must not silently move the repository
		// off the atomic append-and-stage path. The repository resolves the capability, so hiding it here
		// is the silent downgrade.
		IEventStore decorated = Tiered(new TransactionalStore(), A.Fake<IColdEventStore>());

		decorated.GetService(typeof(ITransactionalEventStore))
			.ShouldNotBeNull("the hot store is transactional, so tiered storage must not strip the capability");
	}

	[Fact]
	public void NotAdvertiseTheTransactionalCapabilityThroughTheTieredDecoratorOverAPlainHotStore()
	{
		IEventStore decorated = Tiered(new PlainStore(), A.Fake<IColdEventStore>());

		decorated.GetService(typeof(ITransactionalEventStore))
			.ShouldBeNull("the hot store cannot stage transactionally, so the decorator must not claim it can");
	}

	[Fact]
	public void NotAdvertiseErasureThroughTheTieredDecoratorEvenWhenTheHotStoreErases()
	{
		// The declare-too-many error. Erasing the hot tier leaves the archived range in cold untouched,
		// so answering the erasure probe would promise a guarantee this decorator cannot keep.
		IEventStore decorated = Tiered(new ErasingStore(), A.Fake<IColdEventStore>());

		decorated.GetService(typeof(IEventStoreErasure)).ShouldBeNull();
	}

	[Fact]
	public async Task PerformTheTransactionalAppendOnTheHotStoreThroughTheTieredDecorator()
	{
		var hot = new TransactionalStore();
		IEventStore decorated = Tiered(hot, A.Fake<IColdEventStore>());

		var capability = (ITransactionalEventStore)decorated.GetService(typeof(ITransactionalEventStore))!;

		_ = await capability.AppendWithOutboxStagingAsync(
			"agg-1", "Agg", [], 0, static (_, _) => ValueTask.CompletedTask, TestContext.Current.CancellationToken);

		hot.TransactionalAppends.ShouldBe(1, "writes go to the hot tier, as the ordinary append does");
	}

	[Fact]
	public async Task ReadArchivedEventsThroughTheTieredCapabilityView()
	{
		// The sharp case. Versions 1-4 have been archived to cold and deleted from hot. A capability view
		// that read the bare hot store would return a history missing them, and the caller could not tell.
		var hot = new TransactionalStore { Events = Versions(5, 6, 7) };
		var cold = A.Fake<IColdEventStore>();
		_ = A.CallTo(() => cold.ReadAsync(A<KeyedTenantPartition>._, "agg-1", 0L, A<CancellationToken>._))
			.Returns(Versions(1, 2, 3, 4));

		var view = (IEventStore)Tiered(hot, cold).GetService(typeof(ITransactionalEventStore))!;

		var history = await view.LoadAsync("agg-1", "Agg", TestContext.Current.CancellationToken);

		history.Count.ShouldBe(7, "the view must consult both tiers, not hand back the bare hot store");
		history[0].Version.ShouldBe(1);
		history[^1].Version.ShouldBe(7);
	}

	// -----------------------------------------------------------------------------------------------
	// EncryptingEventStoreDecorator. Its capability is an append, so an unwrapped one writes plaintext
	// through the very object that exists to prevent that.
	// -----------------------------------------------------------------------------------------------

	[Fact]
	public void ExposeTheTransactionalCapabilityThroughTheEncryptingDecorator()
	{
		IEventStore decorated = Encrypting(new TransactionalStore(), A.Fake<IFieldEncryptor>());

		decorated.GetService(typeof(ITransactionalEventStore))
			.ShouldNotBeNull("the decorated store is transactional, so the decorator must expose it");
	}

	[Fact]
	public void NotAdvertiseTheTransactionalCapabilityThroughTheEncryptingDecoratorOverAPlainStore()
	{
		IEventStore decorated = Encrypting(new PlainStore(), A.Fake<IFieldEncryptor>());

		decorated.GetService(typeof(ITransactionalEventStore)).ShouldBeNull();
	}

	[Fact]
	public async Task EncryptEventsAppendedThroughTheEncryptingCapabilityView()
	{
		// Write through the capability view, then look at what actually reached storage. A view that
		// forwarded the inner capability raw would leave the personal field in plaintext.
		var inner = new TransactionalStore();
		var fieldEncryptor = A.Fake<IFieldEncryptor>();
		_ = A.CallTo(() => fieldEncryptor.EncryptAsync("subject-1", A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.Returns(Envelope());

		var capability = (ITransactionalEventStore)
			Encrypting(inner, fieldEncryptor).GetService(typeof(ITransactionalEventStore))!;

		var subjectEvent = new SubjectEvent();
		_ = await capability.AppendWithOutboxStagingAsync(
			"agg-1", "Agg", [subjectEvent], 0, static (_, _) => ValueTask.CompletedTask,
			TestContext.Current.CancellationToken);

		var stored = inner.Staged.ShouldHaveSingleItem().ShouldBeOfType<SubjectEvent>();
		stored.Email.ShouldNotBe(SubjectEvent.PlaintextEmail, "the store must never receive the plaintext");
		EncryptedData.IsFieldEncrypted(Convert.FromBase64String(stored.Email))
			.ShouldBeTrue("the field that reached storage must be a ciphertext envelope");
	}

	// --- Helpers ---

	private static IEventStore Tiered(IEventStore hot, IColdEventStore cold) =>
		new TieredEventStoreDecorator(
			hot, cold, NullLogger<TieredEventStoreDecorator>.Instance, TestTenantContext.SingleTenantDefault);

	private static IEventStore Encrypting(IEventStore inner, IFieldEncryptor fieldEncryptor) =>
		new EncryptingEventStoreDecorator(
			inner,
			A.Fake<IEncryptionProviderRegistry>(),
			new SubjectFieldCryptor(fieldEncryptor),
			A.Fake<IEventSerializer>(),
			Options.Create(new EncryptionOptions { Mode = EncryptionMode.EncryptAndDecrypt }));

	private static IReadOnlyList<StoredEvent> Versions(params long[] versions) =>
	[
		.. versions.Select(v => new StoredEvent(
			EventId: $"evt-{v}",
			AggregateId: "agg-1",
			AggregateType: "Agg",
			EventType: "TestEvent",
			EventData: [],
			Metadata: null,
			Version: v,
			Timestamp: DateTimeOffset.UnixEpoch))
	];

	private static EncryptedData Envelope() => new()
	{
		Ciphertext = [9, 9, 9],
		KeyId = "subject-key",
		KeyVersion = 1,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
	};

	/// <summary>An event carrying a data subject's personal field, so encryption of it is observable.</summary>
	[MessageName("Test.SubjectEvent")]
	private sealed record SubjectEvent : IDomainEvent
	{
		internal const string PlaintextEmail = "subject@example.test";

		public string EventId { get; init; } = "evt-1";

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;


		public IDictionary<string, object>? Metadata { get; init; }

		[DataSubjectId]
		public string SubjectId { get; init; } = "subject-1";

		[PersonalData]
		public string Email { get; set; } = PlaintextEmail;
	}

	private class PlainStore : IEventStore
	{
		/// <summary>The history this store holds. Seeded to model a hot tier whose early events were archived away.</summary>
		public IReadOnlyList<StoredEvent> Events { get; init; } = [];

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(Events);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult(Events);

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(AppendResult.CreateSuccess(0, 0));
	}

	private sealed class TransactionalStore : PlainStore, ITransactionalEventStore
	{
		public int TransactionalAppends { get; private set; }

		/// <summary>The events this store was actually handed, so a caller can see what reached storage.</summary>
		public List<IDomainEvent> Staged { get; } = [];

		public ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken)
		{
			TransactionalAppends++;
			Staged.AddRange(events);
			return ValueTask.FromResult(AppendResult.CreateSuccess(0, 0));
		}
	}

	private sealed class ErasingStore : PlainStore, IEventStoreErasure
	{
		public Task<int> EraseEventsAsync(
			string aggregateId, string aggregateType, Guid erasureRequestId, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<bool> IsErasedAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			Task.FromResult(false);
	}

	/// <summary>Stands in for a telemetry or metrics decorator: measures nothing here, imposes no invariant.</summary>
	private sealed class PassThroughObservationalStore(IEventStore inner)
		: Decorators.DelegatingEventStore(inner);
}
