// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Structural guard: a registration file that resolves <c>ITenantContext</c> to build a persistence store
/// must do so through <c>AddTenantAwareStore</c>, the seam that emits the matching capability marker
/// inseparably from the wiring, rather than by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A hand-rolled registration that resolves <c>ITenantContext</c> itself and
/// passes it into a store's constructor supplies isolation correctly -- the store genuinely reads the
/// ambient tenant -- but emits no <c>ITenantScopingCapability&lt;TContract&gt;</c> marker, because only
/// <c>AddTenantAwareStore</c> emits that marker. The multi-tenancy floor then reads "contract present, no
/// attestation" and refuses a host that is actually safe: a false negative caused by bypassing the seam,
/// not by a real isolation gap.
/// </para>
/// <para>
/// <b>Why the pattern is matched on the RESOLUTION CALL, not a registration shape.</b> Three prior manual
/// sweeps for this class of bug each used a shape-dependent pattern and each was defeated by a shape the
/// previous one did not cover: a single-line pattern missed a construct spanning several lines; a
/// multiline pattern requiring an expression-bodied lambda (<c>sp =&gt; new X(...)</c>) missed a
/// block-bodied lambda (<c>sp =&gt; { var f = ...; return new X(..., sp.GetRequiredService...); }</c>).
/// Every one of those shapes still contains the literal text <c>GetRequiredService&lt;ITenantContext&gt;</c>
/// somewhere in the file, because that is the one call every shape must make to obtain the value at all.
/// Matching on that call, rather than on how it is wrapped, is shape-invariant by construction: it does
/// not need to anticipate a fourth shape nobody has written yet.
/// </para>
/// <para>
/// <b>Both arms, deliberately, plus a third against staleness.</b> The safety arm proves a hand-rolled
/// resolution with no seam call is flagged, in more than one lambda shape. The liveness arm proves a
/// seamed registration, and a file that never touches <c>ITenantContext</c> at all, are not flagged. A
/// third test proves every allowlist entry still exists on disk and still actually matches the pattern it
/// exists to suppress -- an allowlist entry for a file that was deleted, or that no longer resolves the
/// context, is silently doing nothing, which is the same vacuity trap as a scanner that never matches.
/// </para>
/// <para>
/// <b>Exhaustiveness caveat, stated rather than implied.</b> Every allowlisted file was verified, by
/// reading it, to resolve <c>ITenantContext</c> for a reason other than hand-constructing a
/// <c>[TenantOwned]</c>-contract store: reading the ambient tenant to validate consistency or populate a
/// message context, registering a shard-lookup resolver rather than the store itself, re-binding an
/// already-attested keyed family. There is no longer any entry excused on the grounds that its contract
/// is not <c>[TenantOwned]</c>: the last one, the poison-message registration, was closed by routing both
/// of its call sites through the seam rather than by widening this list. A future file added to the
/// allowlist without that verification would weaken this guard exactly as a stale entry does, silently.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantContextResolvedWithoutSeamShould
{
	private static readonly Regex ResolvesTenantContext = new(
		@"Get(Required)?Service<\s*ITenantContext\s*>",
		RegexOptions.Compiled);

	/// <summary>
	/// The registration verbs that emit a tenancy capability marker inseparably from the wiring. A file
	/// calling ANY of them is resolving the context inside a seam, not around one.
	/// </summary>
	/// <remarks>
	/// There are two, and the guard read only the first until the projection seam started requiring its
	/// callers to resolve the context themselves. Before that, the projection seam HANDED the resolved
	/// context to its factory as a second lambda parameter — which is precisely the shape a call site
	/// could bind to a discard, emitting a marker for a store that never read the tenant. Closing that
	/// hole moved the resolution call into the call site, where this guard's shape-invariant pattern
	/// correctly sees it; the guard's own token list was the thing that had not caught up. Recording it
	/// here because "the list of seams" is exactly the kind of implicit single-element set that goes
	/// stale silently: a third seam added later, and not added here, makes every one of its call sites a
	/// false positive.
	/// </remarks>
	private static readonly string[] SeamCallTokens =
	[
		"AddTenantAwareStore",
		"AddTenantScopedProjectionStore",
	];

	/// <summary>
	/// Files where <c>ITenantContext</c> is resolved for a reason other than constructing a
	/// <c>[TenantOwned]</c>-contract store through the seam, each verified by reading it.
	/// </summary>
	private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
	{
		// The seam's own implementation: it resolves ITenantContext to hand it, dep-gated, into the store
		// it constructs. There is nothing above this seam left to call.
		"src/Dispatch/Excalibur.Dispatch.Abstractions/DependencyInjection/TenantScopedStoreServiceCollectionExtensions.cs",

		// Reads the ambient tenant to validate options consistency; does not construct a store.
		"src/Dispatch/Excalibur.Dispatch.Abstractions/ContextValues/TenantContextConsistencyValidator.cs",

		// Reads ITenantContext.TenantId to populate the dispatch message context; does not construct a store.
		"src/Dispatch/Excalibur.Dispatch.Hosting.AspNetCore/DispatcherWebExtensions.cs",

		// Reads ITenantContext.TenantId (nullable form, falling back to the default tenant) to snapshot a
		// value onto ActivityContext; does not construct a store.
		"src/Excalibur/Excalibur.A3/Audit/AuditServiceCollectionExtensions.cs",

		// Sharding resolvers register ITenantStoreResolver<IEventStore>, a shard-lookup cache keyed by
		// shard id -- not the store itself. ITenantStoreResolver<T> carries no [TenantOwned] attribute.
		"src/Excalibur/Excalibur.EventSourcing.CosmosDb/Sharding/CosmosDbTenantShardingExtensions.cs",
		"src/Excalibur/Excalibur.EventSourcing.DynamoDb/Sharding/DynamoDbTenantShardingExtensions.cs",
		"src/Excalibur/Excalibur.EventSourcing.Firestore/Sharding/FirestoreTenantShardingExtensions.cs",
		"src/Excalibur/Excalibur.EventSourcing.MongoDB/Sharding/MongoDbTenantShardingExtensions.cs",
		"src/Excalibur/Excalibur.EventSourcing.Postgres/Sharding/PostgresTenantShardingExtensions.cs",
		"src/Excalibur/Excalibur.EventSourcing.SqlServer/Sharding/SqlServerTenantShardingExtensions.cs",

		// Re-binds the keyed "default" IEventStore to a read-through decorator wrapping the ALREADY hot
		// store the consumer registered (e.g. via UseSqlServer/UsePostgres), which emitted the
		// ITenantScopingCapability<IEventStore> family marker through this same seam at ITS OWN
		// registration. This file constructs no fresh, unattested store.
		"src/Excalibur/Excalibur.EventSourcing/DependencyInjection/TieredStorageServiceCollectionExtensions.cs",

		// Conformance test-kit harness code: reads the ambient tenant from an already-built provider to
		// verify the harness itself is wired correctly, not a production registration.
		"src/Excalibur/Excalibur.Testing.Conformance/Conformance/EventStoreConformanceTestKit.cs",
		"src/Excalibur/Excalibur.Testing.Conformance/Conformance/SagaStoreConformanceTestKit.cs",
	};

	/// <summary>
	/// Returns the path of every source file that resolves <c>ITenantContext</c> without also calling the
	/// seam that ties that resolution to a capability attestation.
	/// </summary>
	internal static IReadOnlyList<string> FindHandSuppliedWithoutSeam(IEnumerable<SourceFile> sources)
	{
		ArgumentNullException.ThrowIfNull(sources);

		var violations = new List<string>();
		foreach (var file in sources)
		{
			if (!ResolvesTenantContext.IsMatch(file.Text))
			{
				continue;
			}

			if (Array.Exists(SeamCallTokens, token => file.Text.Contains(token, StringComparison.Ordinal)))
			{
				continue;
			}

			violations.Add(file.Path);
		}

		return violations;
	}

	// ---------- fixtures ----------

	private static readonly SourceFile SingleLineBypass = new(
		"src/Some.Package/SomeStoreServiceCollectionExtensions.cs",
		"""
		public static class SomeStoreServiceCollectionExtensions
		{
			public static IServiceCollection AddSomeStore(this IServiceCollection services)
			{
				services.TryAddSingleton(sp => new SomeStore(
					sp.GetRequiredService<ITenantContext>(),
					sp.GetRequiredService<ILogger<SomeStore>>()));
				return services;
			}
		}
		""");

	private static readonly SourceFile BlockBodiedLambdaBypass = new(
		"src/Some.Package/OtherStoreServiceCollectionExtensions.cs",
		"""
		public static class OtherStoreServiceCollectionExtensions
		{
			public static IServiceCollection AddOtherStore(this IServiceCollection services)
			{
				services.TryAddSingleton(sp =>
				{
					var factory = sp.GetRequiredService<ISomeFactory>();
					return new OtherStore(factory, sp.GetRequiredService<ITenantContext>());
				});
				return services;
			}
		}
		""");

	private static readonly SourceFile SeamedRegistration = new(
		"src/Some.Package/SeamedStoreServiceCollectionExtensions.cs",
		"""
		public static class SeamedStoreServiceCollectionExtensions
		{
			public static IServiceCollection AddSeamedStore(this IServiceCollection services)
			{
				services.AddTenantAwareStore<ISeamedStore, SeamedStore>();
				services.TryAddSingleton<ISeamedStore>(sp => sp.GetRequiredService<SeamedStore>());
				return services;
			}
		}
		""");

	private static readonly SourceFile ProjectionSeamedRegistration = new(
		"src/Some.Package/SeamedProjectionStoreServiceCollectionExtensions.cs",
		"""
		public static class SeamedProjectionStoreServiceCollectionExtensions
		{
			public static IServiceCollection AddSeamedProjectionStore<TProjection>(this IServiceCollection services)
			{
				services.AddTenantScopedProjectionStore<IProjectionStore<TProjection>, SeamedProjectionStore<TProjection>, IProjectionStore<object>>(
					sp => new SeamedProjectionStore<TProjection>(sp.GetRequiredService<ITenantContext>()));
				return services;
			}
		}
		""");

	private static readonly SourceFile NoTenantContextAtAll = new(
		"src/Some.Package/UntenantedServiceCollectionExtensions.cs",
		"""
		public static class UntenantedServiceCollectionExtensions
		{
			public static IServiceCollection AddUntenantedThing(this IServiceCollection services) => services;
		}
		""");

	// ---------- SAFETY: a hand-rolled resolution with no seam call is flagged, in more than one shape ----------

	[Fact]
	public void Flag_a_single_line_hand_rolled_resolution_with_no_seam_call()
	{
		var violations = FindHandSuppliedWithoutSeam([SingleLineBypass]);

		violations.ShouldBe([SingleLineBypass.Path]);
	}

	[Fact]
	public void Flag_a_block_bodied_lambda_hand_rolled_resolution_too()
	{
		// The exact shape that defeated an earlier expression-lambda-only pattern at this seam: two saga
		// builders resolved the context inside a block-bodied factory lambda and were missed.
		var violations = FindHandSuppliedWithoutSeam([BlockBodiedLambdaBypass]);

		violations.ShouldBe([BlockBodiedLambdaBypass.Path]);
	}

	// ---------- LIVENESS: a seamed registration, or a file that never touches the context, is not flagged ----------

	[Fact]
	public void Not_flag_a_registration_that_calls_the_seam()
	{
		var violations = FindHandSuppliedWithoutSeam([SeamedRegistration]);

		violations.ShouldBeEmpty();
	}

	[Fact]
	public void Not_flag_a_registration_that_calls_the_projection_seam()
	{
		// The projection seam emits the same marker family, so a call site resolving the context inside its
		// factory is seamed, not hand-rolled. Without this arm the guard reports every provider that wires a
		// projection store — a false positive on correct code, which is how a guard gets weakened or deleted.
		var violations = FindHandSuppliedWithoutSeam([ProjectionSeamedRegistration]);

		violations.ShouldBeEmpty();
	}

	[Fact]
	public void Not_flag_a_file_that_never_resolves_the_tenant_context()
	{
		var violations = FindHandSuppliedWithoutSeam([NoTenantContextAtAll]);

		violations.ShouldBeEmpty();
	}

	// ---------- NON-VACUITY + real repository ----------

	[Fact]
	public void Find_zero_unallowlisted_violations_in_the_shipped_source_tree()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(
				Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
				File.ReadAllText(path)))
			.ToList();

		sources.ShouldNotBeEmpty("the source population must be non-empty or the verdict below is vacuous");

		var violations = FindHandSuppliedWithoutSeam(sources)
			.Where(path => !Allowlist.Contains(path))
			.ToList();

		violations.ShouldBeEmpty(
			"each of these files resolves ITenantContext to build a store without calling AddTenantAwareStore or "
			+ "AddTenantScopedProjectionStore, "
			+ "so a store it constructs can be genuinely tenant-scoped while emitting no capability marker for "
			+ "it -- convert the registration to the seam, or add a verified allowlist entry explaining why "
			+ "not: " + string.Join(", ", violations));
	}

	[Fact]
	public void Prove_every_allowlist_entry_still_exists_and_would_be_flagged_without_it()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		foreach (var relativePath in Allowlist)
		{
			var fullPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

			File.Exists(fullPath).ShouldBeTrue(
				$"allowlisted path '{relativePath}' no longer exists on disk -- remove the stale entry");

			var text = File.ReadAllText(fullPath);
			ResolvesTenantContext.IsMatch(text).ShouldBeTrue(
				$"allowlisted path '{relativePath}' no longer resolves ITenantContext -- remove the stale entry, "
				+ "it is suppressing nothing");
		}
	}
}
