// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;
using System.Reflection;

using Excalibur.Data.CloudNative;

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
/// in-memory) OR a change-feed-based <see cref="ICloudNativeOutboxStore"/> (Cosmos DB, DynamoDB,
/// Firestore) — never both. The two are intentionally separate abstractions (see the doc on
/// <see cref="ICloudNativeOutboxStore"/>).
/// </para>
/// <para>
/// <strong>Optional capabilities are consumed via runtime <c>is</c>-checks with documented graceful
/// fallback</strong>, so a store legitimately not implementing one is a coherent (not broken) state:
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
/// optional capability, confirm a documented graceful consumer handles its absence (as the three above do);
/// if nothing guards the absence, that is an advertised-but-unwired gap that must be wired or the capability
/// dropped — not left silent.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
	private static readonly Type[] CapabilityVocabulary =
	[
		typeof(ICloudNativeOutboxStore),
		typeof(ICloudNativeOutboxStoreBatch),
		typeof(IMultiTransportOutboxStore),
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
		new("InMemory", typeof(global::Excalibur.Outbox.InMemory.InMemoryOutboxStore), OutboxFamily.Traditional, []),
		new("Postgres", typeof(global::Excalibur.Outbox.Postgres.PostgresOutboxStore), OutboxFamily.Traditional, []),
		new("Oracle", typeof(global::Excalibur.Outbox.Oracle.OracleOutboxStore), OutboxFamily.Traditional, []),
		new("Redis", typeof(global::Excalibur.Outbox.Redis.RedisOutboxStore), OutboxFamily.Traditional, []),
		new("MongoDb", typeof(global::Excalibur.Outbox.MongoDB.MongoDbOutboxStore), OutboxFamily.Traditional, []),
		new("Elasticsearch", typeof(global::Excalibur.Outbox.ElasticSearch.ElasticsearchOutboxStore), OutboxFamily.Traditional, []),
		new("SqlServer", typeof(global::Excalibur.Outbox.SqlServer.SqlServerOutboxStore), OutboxFamily.Traditional,
			[typeof(IMultiTransportOutboxStore)]),

		// -- Cloud-native ICloudNativeOutboxStore family --
		new("CosmosDb", typeof(global::Excalibur.Outbox.CosmosDb.CosmosDbOutboxStore), OutboxFamily.CloudNative,
			[typeof(ICloudNativeOutboxStore), typeof(ICloudNativeOutboxStoreBatch)]),
		new("DynamoDb", typeof(global::Excalibur.Outbox.DynamoDb.DynamoDbOutboxStore), OutboxFamily.CloudNative,
			[typeof(ICloudNativeOutboxStore), typeof(ICloudNativeOutboxStoreBatch)]),
		new("Firestore", typeof(global::Excalibur.Outbox.Firestore.FirestoreOutboxStore), OutboxFamily.CloudNative,
			[typeof(ICloudNativeOutboxStore), typeof(ICloudNativeOutboxStoreBatch)]),
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
	/// fails here, so cross-provider conformance cannot be silently bypassed.
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
		Matrix.Length.ShouldBe(10, "the outbox backing-store family currently has 10 concrete providers.");

		// Positive controls — a capability that IS implemented is detected.
		typeof(IMultiTransportOutboxStore).IsAssignableFrom(Resolve("SqlServer").StoreType).ShouldBeTrue();
		typeof(ICloudNativeOutboxStore).IsAssignableFrom(Resolve("CosmosDb").StoreType).ShouldBeTrue();

		// Negative controls — a capability that is NOT implemented is correctly reported absent.
		typeof(IMultiTransportOutboxStore).IsAssignableFrom(Resolve("InMemory").StoreType).ShouldBeFalse();
		typeof(ICloudNativeOutboxStore).IsAssignableFrom(Resolve("Postgres").StoreType).ShouldBeFalse();
		typeof(IOutboxStore).IsAssignableFrom(Resolve("CosmosDb").StoreType).ShouldBeFalse(
			"cloud-native stores are intentionally NOT IOutboxStore — the two families are separate.");

		// The cloud-native stores (Cosmos/DynamoDb/Firestore) DECLARE ICloudNativeOutboxStoreBatch — the
		// batch capability their providers were built for is now reachable, so CloudNativeOutboxStore-
		// Extensions' is-check routes to the real batch path rather than the per-item fallback. Coherence:
		// every CloudNative-family store advertises it; no Traditional-family store may.
		Matrix.Where(e => e.Family == OutboxFamily.CloudNative)
			.ShouldAllBe(e => e.ExpectedCapabilities.Contains(typeof(ICloudNativeOutboxStoreBatch)));
		Matrix.Where(e => e.Family == OutboxFamily.Traditional)
			.ShouldAllBe(e => !e.ExpectedCapabilities.Contains(typeof(ICloudNativeOutboxStoreBatch)));
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
