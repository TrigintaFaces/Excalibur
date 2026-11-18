// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Linq;
using System.Reflection;

using Excalibur.Data.CloudNative;
using Excalibur.Data;
using Excalibur.Outbox.Oracle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Conformance.Persistence;

/// <summary>
/// Cross-provider CONFORMANCE MATRIX gate for the outbox backing-store family.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Intent.</strong> Cross-provider consistency must be a GATE the Nth provider has to pass, not a
/// sampled review. This test freezes, for every concrete backing outbox store, which family it belongs to
/// and which optional capabilities it advertises — then asserts the running assemblies still match that
/// frozen matrix. Adding a new provider (or changing an existing one's capability footprint) without
/// updating the matrix fails the build here, forcing a conscious decision rather than silent drift.
/// </para>
/// <para>
/// <strong>Two mutually-exclusive families.</strong> A backing store is EITHER a traditional
/// polling-based <see cref="IOutboxStore"/> (SQL Server, Postgres, Oracle, Redis, Mongo, Elasticsearch,
/// in-memory, Marten) OR a change-feed-based <see cref="ICloudNativeOutboxStore"/> (Cosmos DB, DynamoDB,
/// Firestore) — never both. The two are intentionally separate abstractions (see the doc on
/// <see cref="ICloudNativeOutboxStore"/>).
/// </para>
/// <para>
/// <strong>Declarations are not instance availability or behavioral certification.</strong> Polling
/// workers discover acquisition-scoped capabilities through <c>IOutboxStore.GetService</c>. The default
/// processor requires plain claim completion and dead-letter completion; fenced topologies require the
/// corresponding fenced capabilities. Missing required capabilities fail closed rather than downgrading
/// to process-only completion. Marten deliberately lacks dead-letter completion; staging and a publisher
/// route without store-terminal dead-letter have different prerequisites. Oracle additionally hides all
/// worker capabilities unless an owned worker connection source is configured. The instance controls
/// below exercise these availability differences without opening a database.
/// </para>
/// <para>
/// Other optional capabilities have consumer-specific handling:
/// </para>
/// <list type="bullet">
/// <item><see cref="IMultiTransportOutboxStore"/> — per-transport fan-out delivery tracking; only stores
///   that need it implement it.</item>
/// <item><see cref="ICloudNativeOutboxStoreBatch"/> — <c>CloudNativeOutboxStoreExtensions</c> does
///   <c>if (store is ICloudNativeOutboxStoreBatch batch)</c> and otherwise returns a no-op batch result.</item>
/// </list>
/// <para>
/// <strong>For a future provider author:</strong> when you add an <c>Excalibur.Outbox.&lt;Provider&gt;</c>
/// backing store you MUST add its entry to <see cref="Matrix"/> below (family + capability footprint), or
/// <see cref="Matrix_CoversEveryConcreteBackingStore"/> fails. If your provider deliberately omits an
/// optional capability, confirm the documented consumer handles its absence, including startup refusal
/// for a required worker capability. If nothing guards the absence, that is an advertised-but-unwired gap that must be wired or the capability
/// dropped — not left silent.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class OutboxCapabilityMatrixShould
{
	private enum OutboxFamily
	{
		/// <summary>Polling-based <see cref="IOutboxStore"/> stores.</summary>
		Traditional,

		/// <summary>Change-feed-based <see cref="ICloudNativeOutboxStore"/> stores.</summary>
		CloudNative,
	}

	/// <summary>
	/// The optional capability-interface vocabulary the matrix tracks. Each store's actual footprint over
	/// this set is compared (by reflection) against its frozen <see cref="ProviderEntry.ExpectedCapabilities"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The three fencing capabilities are in this vocabulary because they decide whether a host STARTS.</b>
	/// The outbox composition guard refuses to start a host that registers a leader election over a store
	/// which cannot fence, so gaining or losing one of them is not a quiet capability change - it is the
	/// difference between a supported deployment and a startup refusal. Before they were tracked here,
	/// that population lived only in a class declaration nobody diffed, and three stores could drift out of
	/// it without any gate moving.
	/// </para>
	/// <para>
	/// <b>They are also separately losable, which is why all three are listed rather than one.</b>
	/// <see cref="IFencedOutboxStore"/> fences only the claim and the mark-sent; the completion path - the
	/// failure report and the dead-letter transition - is fenced by the other two, and a store that offers
	/// the first alone closes half the window while reading as though it closed all of it.
	/// </para>
	/// </remarks>
	private static readonly Type[] CapabilityVocabulary =
	[
		typeof(ICloudNativeOutboxStore),
		typeof(ICloudNativeOutboxStoreBatch),
		typeof(ICloudNativeOutboxStoreClaim),
		typeof(IMultiTransportOutboxStore),
		typeof(IClaimScopedOutboxStore),
		typeof(IClaimScopedDeadLetterableOutboxStore),
		typeof(IFencedOutboxStore),
		typeof(IFencedClaimScopedOutboxStore),
		typeof(IFencedDeadLetterableOutboxStore),
	];

	private sealed record ProviderEntry(
		string Key,
		Type StoreType,
		OutboxFamily Family,
		Type[] ExpectedCapabilities);

	/// <summary>
	/// The FROZEN capability matrix. One entry per concrete backing outbox store. Decorators
	/// (encrypting/telemetry) are intentionally excluded — they wrap a store, they are not a backing store.
	/// </summary>
	private static readonly ProviderEntry[] Matrix =
	[
		// -- Traditional IOutboxStore family --
		new("InMemory", typeof(global::Excalibur.Outbox.InMemory.InMemoryOutboxStore), OutboxFamily.Traditional,
			[
				typeof(IClaimScopedOutboxStore),
				typeof(IClaimScopedDeadLetterableOutboxStore),
				typeof(IFencedOutboxStore),
				typeof(IFencedClaimScopedOutboxStore),
				typeof(IFencedDeadLetterableOutboxStore)
			]),
		new("Postgres", typeof(global::Excalibur.Outbox.Postgres.PostgresOutboxStore), OutboxFamily.Traditional,
			[
				typeof(IClaimScopedOutboxStore),
				typeof(IClaimScopedDeadLetterableOutboxStore),
				typeof(IFencedOutboxStore),
				typeof(IFencedClaimScopedOutboxStore),
				typeof(IFencedDeadLetterableOutboxStore)
			]),
		new("Oracle", typeof(global::Excalibur.Outbox.Oracle.OracleOutboxStore), OutboxFamily.Traditional,
			[
				typeof(IClaimScopedOutboxStore),
				typeof(IClaimScopedDeadLetterableOutboxStore),
				typeof(IFencedOutboxStore),
				typeof(IFencedClaimScopedOutboxStore),
				typeof(IFencedDeadLetterableOutboxStore)
			]),
		new("Redis", typeof(global::Excalibur.Outbox.Redis.RedisOutboxStore), OutboxFamily.Traditional,
			[typeof(IClaimScopedOutboxStore), typeof(IClaimScopedDeadLetterableOutboxStore)]),
		new("MongoDb", typeof(global::Excalibur.Outbox.MongoDB.MongoDbOutboxStore), OutboxFamily.Traditional,
			[typeof(IClaimScopedOutboxStore), typeof(IClaimScopedDeadLetterableOutboxStore)]),
		// Redis and MongoDb declare NO fenced capability, and that is the frozen fact rather than an
		// omission. Both were removed from IFencedOutboxStore because neither substrate can carry the
		// contract: a fence needs its high-water to be monotone across failover, and an asynchronously
		// replicated value can roll backwards. Restoring an entry here without restoring the interface
		// would re-freeze a capability the stores do not have.
		new("Elasticsearch", typeof(global::Excalibur.Outbox.ElasticSearch.ElasticsearchOutboxStore), OutboxFamily.Traditional,
			[typeof(IClaimScopedOutboxStore), typeof(IClaimScopedDeadLetterableOutboxStore)]),
		new("SqlServer", typeof(global::Excalibur.Outbox.SqlServer.SqlServerOutboxStore), OutboxFamily.Traditional,
			[
				typeof(IMultiTransportOutboxStore),
				typeof(IClaimScopedOutboxStore),
				typeof(IClaimScopedDeadLetterableOutboxStore),
				typeof(IFencedOutboxStore),
				typeof(IFencedClaimScopedOutboxStore),
				typeof(IFencedDeadLetterableOutboxStore)
			]),

		// Marten's C-only footprint is deliberate: no store-terminal DLQ or durable fencing is advertised.
		new("Marten", typeof(global::Excalibur.Outbox.Marten.MartenOutboxStore), OutboxFamily.Traditional,
			[typeof(IClaimScopedOutboxStore)]),

		// -- Cloud-native ICloudNativeOutboxStore family --
		new("CosmosDb", typeof(global::Excalibur.Outbox.CosmosDb.CosmosDbOutboxStore), OutboxFamily.CloudNative,
			[
				typeof(ICloudNativeOutboxStore),
				typeof(ICloudNativeOutboxStoreBatch),
				typeof(ICloudNativeOutboxStoreClaim)
			]),
		new("DynamoDb", typeof(global::Excalibur.Outbox.DynamoDb.DynamoDbOutboxStore), OutboxFamily.CloudNative,
			[
				typeof(ICloudNativeOutboxStore),
				typeof(ICloudNativeOutboxStoreBatch),
				typeof(ICloudNativeOutboxStoreClaim)
			]),
		new("Firestore", typeof(global::Excalibur.Outbox.Firestore.FirestoreOutboxStore), OutboxFamily.CloudNative,
			[
				typeof(ICloudNativeOutboxStore),
				typeof(ICloudNativeOutboxStoreBatch),
				typeof(ICloudNativeOutboxStoreClaim)
			]),
	];

	public static TheoryData<string> ProviderKeys()
	{
		var data = new TheoryData<string>();
		foreach (var entry in Matrix)
		{
			data.Add(entry.Key);
		}

		return data;
	}

	private static ProviderEntry Resolve(string key) =>
		Matrix.Single(e => e.Key == key);

	/// <summary>
	/// Every backing store belongs to EXACTLY ONE family: it implements <see cref="IOutboxStore"/> XOR
	/// <see cref="ICloudNativeOutboxStore"/>, and that matches its frozen family classification.
	/// </summary>
	[Theory]
	[MemberData(nameof(ProviderKeys))]
	public void Store_BelongsToExactlyOneFamily(string key)
	{
		var entry = Resolve(key);

		var isTraditional = typeof(IOutboxStore).IsAssignableFrom(entry.StoreType);
		var isCloudNative = typeof(ICloudNativeOutboxStore).IsAssignableFrom(entry.StoreType);

		(isTraditional ^ isCloudNative).ShouldBeTrue(
			$"{entry.StoreType.Name} must implement IOutboxStore XOR ICloudNativeOutboxStore " +
			$"(traditional={isTraditional}, cloud-native={isCloudNative}) — a backing store belongs to " +
			"exactly one outbox family.");

		switch (entry.Family)
		{
			case OutboxFamily.Traditional:
				isTraditional.ShouldBeTrue($"{entry.StoreType.Name} is classified Traditional but does not implement IOutboxStore.");
				break;
			case OutboxFamily.CloudNative:
				isCloudNative.ShouldBeTrue($"{entry.StoreType.Name} is classified CloudNative but does not implement ICloudNativeOutboxStore.");
				break;
			default:
				throw new InvalidOperationException($"Unhandled family {entry.Family}.");
		}
	}

	/// <summary>
	/// The store's ACTUAL implemented capabilities (over <see cref="CapabilityVocabulary"/>) must equal its
	/// FROZEN expected footprint. This is the load-bearing, non-vacuous snapshot lock: if a provider silently
	/// gains or loses a capability (e.g. SqlServer drops <see cref="IMultiTransportOutboxStore"/>), this fails until the matrix is
	/// consciously updated.
	/// </summary>
	[Theory]
	[MemberData(nameof(ProviderKeys))]
	public void Store_CapabilityFootprint_MatchesFrozenMatrix(string key)
	{
		var entry = Resolve(key);

		var actual = CapabilityVocabulary
			.Where(cap => cap.IsAssignableFrom(entry.StoreType))
			.OrderBy(t => t.Name, StringComparer.Ordinal)
			.ToArray();

		var expected = entry.ExpectedCapabilities
			.OrderBy(t => t.Name, StringComparer.Ordinal)
			.ToArray();

		actual.Select(t => t.Name).ShouldBe(
			expected.Select(t => t.Name),
			ignoreOrder: false,
			$"{entry.StoreType.Name}: implemented capability set diverged from the frozen matrix. " +
			"Update the matrix entry deliberately (and confirm any newly-absent capability is still guarded " +
			"by a documented graceful consumer).");
	}

	/// <summary>
	/// Coherence: <see cref="ICloudNativeOutboxStoreBatch"/> may only appear alongside
	/// <see cref="ICloudNativeOutboxStore"/> (its contract says implementations "should implement this
	/// alongside" the base). A batch capability on a non-cloud-native store would be incoherent.
	/// </summary>
	[Theory]
	[MemberData(nameof(ProviderKeys))]
	public void Store_CloudBatchCapability_ImpliesCloudNativeBase(string key)
	{
		var entry = Resolve(key);

		if (typeof(ICloudNativeOutboxStoreBatch).IsAssignableFrom(entry.StoreType))
		{
			typeof(ICloudNativeOutboxStore).IsAssignableFrom(entry.StoreType).ShouldBeTrue(
				$"{entry.StoreType.Name} implements ICloudNativeOutboxStoreBatch without ICloudNativeOutboxStore.");
		}
	}

	/// <summary>
	/// THE Nth-PROVIDER GATE. Discovers every concrete backing outbox store present in the loaded
	/// <c>Excalibur.Outbox.&lt;Provider&gt;</c> provider assemblies (excluding the core <c>Excalibur.Outbox</c>
	/// assembly and its decorators) and asserts the set exactly equals the frozen <see cref="Matrix"/>. A new
	/// provider referenced by this test but missing from the matrix — or a matrix entry whose type vanished —
	/// fails here. Discovery is bounded to the matrix provider assemblies: referencing a new assembly alone
	/// does not prove it was loaded or discovered. This declaration gate does not replace real-provider tests.
	/// </summary>
	[Fact]
	public void Matrix_CoversEveryConcreteBackingStore()
	{
		// Force every matrix provider assembly to load (typeof already did, but be explicit for discovery).
		var providerAssemblies = Matrix
			.Select(e => e.StoreType.Assembly)
			.Distinct()
			.ToArray();

		var discovered = providerAssemblies
			.SelectMany(SafeGetTypes)
			.Where(IsConcreteBackingStore)
			.Select(t => t.FullName!)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToArray();

		var expected = Matrix
			.Select(e => e.StoreType.FullName!)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToArray();

		discovered.ShouldBe(
			expected,
			ignoreOrder: false,
			"A concrete backing outbox store in a referenced provider assembly is not covered by the " +
			"capability matrix. Add its entry to OutboxCapabilityMatrixShould.Matrix (family + capabilities).");
	}

	/// <summary>
	/// Non-vacuity + discrimination self-check. Proves the reflection checks above actually distinguish
	/// implemented from unimplemented capabilities (positive AND negative controls), so the gate cannot
	/// pass by trivially matching everything or nothing.
	/// </summary>
	[Fact]
	public void Matrix_IsNonVacuous_AndCapabilityChecksDiscriminate()
	{
		Matrix.Length.ShouldBe(11, "the matrix retains its ten providers and includes the Marten polling provider.");

		// Positive controls — a capability that IS implemented is detected.
		typeof(IMultiTransportOutboxStore).IsAssignableFrom(Resolve("SqlServer").StoreType).ShouldBeTrue();
		typeof(ICloudNativeOutboxStore).IsAssignableFrom(Resolve("CosmosDb").StoreType).ShouldBeTrue();

		// Negative controls — a capability that is NOT implemented is correctly reported absent.
		typeof(IMultiTransportOutboxStore).IsAssignableFrom(Resolve("InMemory").StoreType).ShouldBeFalse();
		typeof(ICloudNativeOutboxStore).IsAssignableFrom(Resolve("Postgres").StoreType).ShouldBeFalse();
		typeof(IOutboxStore).IsAssignableFrom(Resolve("CosmosDb").StoreType).ShouldBeFalse(
			"cloud-native stores are intentionally NOT IOutboxStore — the two families are separate.");

		typeof(IClaimScopedOutboxStore).IsAssignableFrom(Resolve("Marten").StoreType).ShouldBeTrue();
		typeof(IClaimScopedDeadLetterableOutboxStore).IsAssignableFrom(Resolve("Marten").StoreType).ShouldBeFalse();
		typeof(IFencedClaimScopedOutboxStore).IsAssignableFrom(Resolve("Redis").StoreType).ShouldBeFalse();
		typeof(IClaimScopedOutboxStore).IsAssignableFrom(Resolve("CosmosDb").StoreType).ShouldBeFalse();

		// The cloud-native stores (Cosmos/DynamoDb/Firestore) DECLARE ICloudNativeOutboxStoreBatch — the
		// batch capability their providers were built for is now reachable, so CloudNativeOutboxStore-
		// Extensions' is-check routes to the real batch path rather than the per-item fallback. Coherence:
		// every CloudNative-family store advertises it; no Traditional-family store may.
		Matrix.Where(e => e.Family == OutboxFamily.CloudNative)
			.ShouldAllBe(e => e.ExpectedCapabilities.Contains(typeof(ICloudNativeOutboxStoreBatch)));
		Matrix.Where(e => e.Family == OutboxFamily.Traditional)
			.ShouldAllBe(e => !e.ExpectedCapabilities.Contains(typeof(ICloudNativeOutboxStoreBatch)));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Oracle_InstanceWorkerAvailabilityRequiresAnOwnedConnectionSource(bool ownedWorkers)
	{
		var borrowed = A.Fake<IDb>();
		var callbackCalls = 0;
		var expected = new InvalidOperationException("deliberate worker callback failure before any connection exists");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle =>
		{
			oracle.ConnectionFactory(_ => borrowed);
			if (ownedWorkers)
			{
				oracle.WorkerConnectionFactory((_, _) =>
				{
					callbackCalls++;
					throw expected;
				});
			}
		})));
		await using var provider = services.BuildServiceProvider(validateScopes: true);
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("oracle"));
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("default"));
		foreach (var capability in new[]
		{
			typeof(IClaimScopedOutboxStore), typeof(IClaimScopedDeadLetterableOutboxStore),
			typeof(IFencedOutboxStore), typeof(IFencedClaimScopedOutboxStore), typeof(IFencedDeadLetterableOutboxStore),
		})
		{
			capability.IsAssignableFrom(store.GetType()).ShouldBeTrue("a declaration alone cannot prove this instance is worker-admissible");
			if (ownedWorkers) { store.GetService(capability).ShouldBeSameAs(store); }
			else { store.GetService(capability).ShouldBeNull(); }
		}
		store.GetService(typeof(IOutboxStore)).ShouldBeSameAs(store);
		store.GetService(typeof(ICloudNativeOutboxStore)).ShouldBeNull();
		callbackCalls.ShouldBe(0, "capability discovery must not acquire a connection");
		var actual = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await ((IClaimScopedOutboxStore)store).MarkSentAsync("message", "claim", CancellationToken.None));
		if (ownedWorkers)
		{
			actual.ShouldBeSameAs(expected);
			callbackCalls.ShouldBe(1);
		}
		else
		{
			actual.Message.ShouldContain("WorkerConnectionFactory");
			callbackCalls.ShouldBe(0);
		}
		A.CallTo(() => borrowed.Connection).MustNotHaveHappened();
	}

	[Fact]
	public void Marten_InstanceExposesClaimCompletionWithoutInventingDeadLetterOrFencing()
	{
		var documentStore = A.Fake<global::Marten.IDocumentStore>();
		using var concrete = new global::Excalibur.Outbox.Marten.MartenOutboxStore(documentStore,
			global::Microsoft.Extensions.Options.Options.Create(new global::Excalibur.Outbox.Marten.MartenOutboxStoreOptions()),
			NullLogger<global::Excalibur.Outbox.Marten.MartenOutboxStore>.Instance);
		IOutboxStore store = concrete;
		store.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeSameAs(concrete);
		store.GetService(typeof(IOutboxStore)).ShouldBeSameAs(concrete);
		foreach (var missing in new[]
		{
			typeof(IClaimScopedDeadLetterableOutboxStore), typeof(IFencedOutboxStore),
			typeof(IFencedClaimScopedOutboxStore), typeof(IFencedDeadLetterableOutboxStore),
			typeof(ICloudNativeOutboxStore),
		})
		{
			store.GetService(missing).ShouldBeNull();
		}
		Fake.GetCalls(documentStore).ShouldBeEmpty("capability discovery does not open a Marten session or certify database behavior");
	}

	private static bool IsConcreteBackingStore(Type t) =>
		t is { IsClass: true, IsAbstract: false, IsPublic: true }
		&& (typeof(IOutboxStore).IsAssignableFrom(t) || typeof(ICloudNativeOutboxStore).IsAssignableFrom(t));

	private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(t => t is not null)!;
		}
	}
}
