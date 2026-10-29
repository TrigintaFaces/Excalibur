// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// The next generation of the read model <see cref="ConformanceProjection"/>, declared so that a store
/// configured against the same destination resolves somewhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the name carries no digit.</b> Several providers derive the destination from the read model's
/// type name, so two generations named <c>…Projection</c> and <c>…ProjectionV2</c> would resolve apart on
/// the TYPE NAME alone and the generation arm would pass without the generation participating in
/// anything. The name deliberately contains no <c>v2</c> substring, which is what lets the arm assert
/// that the generation segment is present rather than merely that the two names differ.
/// </para>
/// <para>
/// It accumulates for the same reason <see cref="ConformanceProjection"/> does, and carries the same
/// shape so a store that confused the two generations would read one back where the other was written
/// rather than failing to deserialize.
/// </para>
/// </remarks>
[ProjectionVersion(2)]
public sealed class ConformanceProjectionNextGeneration
{
	/// <summary>Gets or sets the projection identifier.</summary>
	/// <value>The identifier the store keys this projection by.</value>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the accumulated total.</summary>
	/// <value>A running total, so a fold landing in the wrong generation is visible in the value.</value>
	public int Total { get; set; }
}

/// <summary>
/// Conformance kit for the destination a projection store addresses: that the store says which one it is,
/// that moving it changes the answer, and that a declared generation sends a read model somewhere new
/// WITHOUT disturbing the generation already serving reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this layer adds over the per-provider unit arms.</b> Every provider already has offline arms
/// binding how it composes a destination name and an identity from its configuration. Those arms are
/// cheap and they are the right place for the composition rules. What they cannot reach is the property
/// the side-by-side upgrade actually rests on: that two generations of one read model, pointed at the
/// same configured destination, really do occupy different storage — so a document written through the
/// previous generation is still readable, unchanged, after the next generation has written its own. That
/// requires a real store writing real documents, which is why it lives here.
/// </para>
/// <para>
/// <b>Scope of the generation arms, stated rather than implied.</b> For a provider whose destination is
/// derived from the read model's type name — a per-type index, a type-keyed discriminator, a
/// per-type subcollection — the two generations would resolve apart even if the generation contributed
/// nothing, because the two CLR types have different names. The generation arm therefore asserts the
/// presence of the generation SEGMENT, not merely that the two identities differ: removing the
/// generation from the provider's composition reddens that conjunct on every provider. The side-by-side
/// arm is the weaker of the two on exactly those providers, because the type name alone would keep the
/// two apart; it still binds the guarantee the epic rests on, and it is the only arm that can fail for
/// a provider that resolves two names and writes to one of them.
/// </para>
/// <para>
/// <b>No arm here skips.</b> Derive this layer only for a store that addresses a physical destination and
/// advertises which one and supplies a generation-reader factory. These are separate optional
/// capabilities; destination identity alone does not promise generation reads. This layer verifies
/// both capabilities together. A store without that combination belongs on
/// <see cref="PositionedProjectionStoreConformanceTestKit"/> alone, so that an absent capability is an
/// absent suite rather than an arm reporting an unverified pass.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class PositionedProjectionDestinationConformanceTestKit : PositionedProjectionStoreConformanceTestKit
{
	/// <summary>
	/// The generation segment the convention contributes for the second generation.
	/// </summary>
	/// <remarks>
	/// HARD-CODED rather than composed through the convention under test. Deriving it would make the arm
	/// agree with whatever that convention currently does, including doing nothing at all.
	/// </remarks>
	private const string SecondGenerationSegment = "v2";

	/// <summary>
	/// Creates a store for <see cref="ConformanceProjection"/> configured against a DIFFERENT destination
	/// than <see cref="PositionedProjectionStoreConformanceTestKit.CreateStoreAsync"/> resolves.
	/// </summary>
	/// <returns>A store whose configured destination differs, and nothing else.</returns>
	/// <remarks>
	/// Change the configured destination — the table, container, collection or index name — through the
	/// provider's real configuration, not by hand-building an identity string. Nothing is written through
	/// this store, so it needs no provisioning; only its advertised identity is read.
	/// </remarks>
	protected abstract Task<IProjectionStore<ConformanceProjection>> CreateRelocatedStoreAsync();

	/// <summary>
	/// Creates a store for <see cref="ConformanceProjectionNextGeneration"/> configured against the SAME
	/// destination as <see cref="PositionedProjectionStoreConformanceTestKit.CreateStoreAsync"/>.
	/// </summary>
	/// <returns>A store differing from the baseline only in the generation its read model declares.</returns>
	/// <remarks>
	/// <b>Configure it identically and provision it explicitly.</b> The point of the arms below is that
	/// the declared generation is the ONLY difference, so a suite that also moves the configured name
	/// proves nothing. Where the provider needs the destination created up front, create it under the
	/// name the generation segment produces, written out literally — a suite that provisions through the
	/// convention under test would follow that convention into a regression and stay green.
	/// </remarks>
	protected abstract Task<IProjectionStore<ConformanceProjectionNextGeneration>> CreateNextGenerationStoreAsync();

	/// <summary>
	/// The store states which destination it is configured against.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The capability is how an operator planning a cutover, and the start-up checks that refuse two read
	/// models pointed at one destination, learn where a store writes. A store that resolves nothing leaves
	/// both blind, and leaves the collision it is meant to detect expressible by configuration.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Advertise_the_destination_it_is_configured_against()
	{
		RecordArmExecuted(nameof(Advertise_the_destination_it_is_configured_against));

		var store = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateStoreAsync returned null.");

		_ = RequireIdentity(store, nameof(CreateStoreAsync));
	}

	/// <summary>
	/// Moving the configured destination changes the identity the store advertises.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>The arm a constant identity fails.</b> An identity composed from the read model's type alone is
	/// nonblank, stable and useless: two stores written to two different tables report the same answer,
	/// so the start-up check that refuses a collision sees one where there is none and misses the one
	/// there is.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Change_the_identity_when_the_configured_destination_moves()
	{
		RecordArmExecuted(nameof(Change_the_identity_when_the_configured_destination_moves));

		var baseline = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateStoreAsync returned null.");
		var relocated = await CreateRelocatedStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateRelocatedStoreAsync returned null.");

		var baselineIdentity = RequireIdentity(baseline, nameof(CreateStoreAsync)).Identity;
		var relocatedIdentity = RequireIdentity(relocated, nameof(CreateRelocatedStoreAsync)).Identity;

		Require(
			!string.Equals(baselineIdentity, relocatedIdentity, StringComparison.Ordinal),
			"two stores configured against different destinations must advertise different identities; "
			+ $"both reported '{baselineIdentity}', so the identity does not depend on the configuration "
			+ "and cannot tell two read models apart that address one destination");
	}

	/// <summary>
	/// A declared generation resolves a distinct destination from the generation before it.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// <b>Two conjuncts, and the second is the one that cannot be satisfied by accident.</b> The
	/// identities must differ, AND the next generation's must carry the generation segment the previous
	/// one does not. The inequality alone is satisfied on any provider that derives its destination from
	/// the read model's type name, because the two generations are two CLR types; only the segment
	/// establishes that the DECLARED GENERATION is what moved the destination.
	/// </para>
	/// <para>
	/// The previous generation's identity is asserted NOT to carry the segment, because the whole safety
	/// property of the convention is that an unversioned read model keeps the destination it addresses
	/// today. A rule that appended a segment unconditionally would rename every destination the framework
	/// addresses, each one folding from zero, each one losing a cursor that was serving correctly.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Resolve_a_distinct_destination_for_the_next_generation()
	{
		RecordArmExecuted(nameof(Resolve_a_distinct_destination_for_the_next_generation));

		var first = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateStoreAsync returned null.");
		var next = await CreateNextGenerationStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateNextGenerationStoreAsync returned null.");

		var firstIdentity = RequireIdentity(first, nameof(CreateStoreAsync)).Identity;
		var nextIdentity = RequireIdentity(next, nameof(CreateNextGenerationStoreAsync)).Identity;

		Require(
			!string.Equals(firstIdentity, nextIdentity, StringComparison.Ordinal),
			"a read model declaring a new generation must address a different destination; both "
			+ $"generations reported '{firstIdentity}', so the new shape would interleave with the old in "
			+ "one destination and the read model would be silently part stale");

		Require(
			!firstIdentity.Contains(SecondGenerationSegment, StringComparison.Ordinal),
			"the generation already serving reads declares no generation and must keep the destination it "
			+ $"addresses today, with no generation segment; it reported '{firstIdentity}'");

		Require(
			nextIdentity.Contains(SecondGenerationSegment, StringComparison.Ordinal),
			$"the next generation's destination must carry its generation segment '{SecondGenerationSegment}'; "
			+ $"it reported '{nextIdentity}'. Two differing identities are not sufficient here: these are "
			+ "two CLR types, so a provider deriving its destination from the type name reports different "
			+ "identities whether the declared generation participates or not");
	}

	/// <summary>
	/// The previous generation keeps serving its own documents while the next one builds.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// <b>The guarantee the whole side-by-side upgrade rests on, and the only arm that can observe it.</b>
	/// A name resolved apart is not storage held apart: a provider may compose two destination names
	/// correctly and then write through a client bound to one of them, or read back through a key that
	/// omits the discriminator it wrote. Both are invisible to any arm that compares strings.
	/// </para>
	/// <para>
	/// The two writes deliberately share one identifier, because a colliding identifier is the only shape
	/// in which a shared destination is observable: distinct identifiers coexist in one destination
	/// perfectly well, so a suite that used them would pass against a provider holding both generations
	/// in the same place.
	/// </para>
	/// <para>
	/// The read model accumulates, so the failure is a WRONG VALUE rather than an absent row — a
	/// provider that overwrote the previous generation returns the next generation's total under the
	/// previous generation's store, which is the silent half-stale read model the generation feature
	/// exists to prevent.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Keep_the_previous_generation_readable_while_the_next_one_builds()
	{
		RecordArmExecuted(nameof(Keep_the_previous_generation_readable_while_the_next_one_builds));

		var first = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var next = await CreateNextGenerationStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateNextGenerationStoreAsync returned null.");

		var nextPositioned = next.GetService(typeof(IPositionedProjectionStore<ConformanceProjectionNextGeneration>))
			as IPositionedProjectionStore<ConformanceProjectionNextGeneration>
			?? throw new ConformanceAssertionException(
				$"The store '{next.GetType().Name}' returned by CreateNextGenerationStoreAsync does not "
				+ "provide IPositionedProjectionStore<T>. The next generation folds from zero and needs "
				+ "the same conditional write the generation it replaces uses.");

		var id = NewId();

		var firstWrite = await first
			.UpsertAtPositionAsync(id, new ConformanceProjection { Id = id, Total = 1 },
				expectedPosition: null, newPosition: 10, CancellationToken.None)
			.ConfigureAwait(false);
		Require(
			firstWrite.Outcome == ProjectionAdvanceOutcome.Applied,
			$"the previous generation's write must land; it reported {firstWrite.Outcome}");

		// The next generation starts from nothing in its OWN destination -- claiming absence is the
		// assertion that it is a fresh destination rather than the one already serving reads. A provider
		// that resolved one destination for both reports Superseded here, which is itself the failure.
		var nextWrite = await nextPositioned
			.UpsertAtPositionAsync(id, new ConformanceProjectionNextGeneration { Id = id, Total = 99 },
				expectedPosition: null, newPosition: 10, CancellationToken.None)
			.ConfigureAwait(false);
		Require(
			nextWrite.Outcome == ProjectionAdvanceOutcome.Applied,
			"the next generation must fold from zero into its own destination, so a write claiming "
			+ $"absence must land; it reported {nextWrite.Outcome}, which is what a store resolving ONE "
			+ "destination for both generations reports — the row was already there");

		var (previous, previousPosition) = await first.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			previous is not null,
			"the previous generation must still be readable while the next one builds; its row was gone "
			+ "after the next generation wrote, so the two generations share one destination and the "
			+ "read model serving live reads was destroyed by an upgrade");

		Require(
			previous?.Total == 1,
			$"the previous generation must return the state IT stored; it returned {previous?.Total} where "
			+ "1 was written. The next generation's fold reached the destination serving live reads, so "
			+ "reads are answered from a shape nobody deployed");

		Require(
			previousPosition.ExpectedPositionOrNull == 10,
			"the previous generation must keep its own cursor while the next one builds; it read "
			+ $"{previousPosition}");

		var nextState = await nextPositioned.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false);
		Require(
			nextState?.Total == 99,
			$"LIVENESS: the next generation must be readable too; it returned {nextState?.Total} where 99 "
			+ "was written. Without this, a store that accepted the write and discarded it would satisfy "
			+ "every assertion above — the cheapest way to leave the previous generation untouched");
        var retiredId = NewId();
        await first.DeleteAsync(retiredId, CancellationToken.None).ConfigureAwait(false);
        var independentWrite = await nextPositioned.UpsertAtPositionAsync(retiredId,
            new ConformanceProjectionNextGeneration { Id = retiredId, Total = 7 }, null, 12,
            CancellationToken.None).ConfigureAwait(false);
        Require(independentWrite.Outcome == ProjectionAdvanceOutcome.Applied,
            "retirement in one generation must not prevent initial population in another");
        Require((await first.GetWithPositionAsync(retiredId, CancellationToken.None).ConfigureAwait(false)).Position.Kind
            == ProjectionPositionKind.Retired, "new generation writes must not replace the old generation marker");

	}

	/// <summary>
	/// A generation reader reports ONE storage scope through both of its members, and addresses the
	/// destination it was opened for.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>The arm a null-returning scope property fails.</b> The handle exposes its scope twice: a
	/// synchronous property and an async resolve. Null means resolution has not completed yet.
	/// After a successful resolve, the property must report that resolved scope; an unresolvable
	/// scope throws from the resolver instead of returning null.
	/// <para>
	/// The last two assertions are the LIVENESS half and they are load-bearing: a handle that refused
	/// every read, or addressed no storage at all, would satisfy the agreement check above while inert.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Report_one_storage_scope_through_both_generation_reader_members()
	{
		RecordArmExecuted(nameof(Report_one_storage_scope_through_both_generation_reader_members));

		var store = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new ConformanceAssertionException("CreateStoreAsync returned null.");

		var destination = RequireIdentity(store, nameof(CreateStoreAsync)).Identity;

		var factory = store.GetService(typeof(IProjectionGenerationReaderFactory<ConformanceProjection>))
				as IProjectionGenerationReaderFactory<ConformanceProjection>
			?? throw new ConformanceAssertionException(
				$"The store '{store.GetType().Name}' does not provide "
				+ "IProjectionGenerationReaderFactory. This conformance layer requires both destination "
				+ "identity and generation reads; use PositionedProjectionStoreConformanceTestKit for "
				+ "stores that do not provide that combination.");

		var id = NewId();
		await store.UpsertAsync(id, new ConformanceProjection { Id = id, Total = 41 }, CancellationToken.None)
			.ConfigureAwait(false);

		await using var reader = factory.OpenReader(destination);

		var resolved = await reader.GetDestinationScopeAsync(CancellationToken.None).ConfigureAwait(false);
		Require(
			!string.IsNullOrWhiteSpace(resolved),
			"a handle opened against a configured destination must be able to establish its storage scope; "
			+ "the resolve returned nothing, which the contract reads as 'cannot be established' and every "
			+ "caller that verifies a recorded scope treats as unverifiable");

		Require(
			string.Equals(reader.DestinationScope, resolved, StringComparison.Ordinal),
			"both members must report the same storage scope; the property returned "
			+ $"'{reader.DestinationScope ?? "null"}' where the resolve returned '{resolved}'. A property "
			+ "that answers null once the scope IS established states the opposite of the truth");

		var read = await reader.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(false);
		Require(
			read?.Total == 41,
			"LIVENESS: the handle must address the destination it was opened for; reading the row just "
			+ $"written there returned {read?.Total.ToString() ?? "nothing"} where 41 was written");

		Require(
			await reader.CountAsync(null, CancellationToken.None).ConfigureAwait(false) >= 1,
			"LIVENESS: a handle that counted nothing would satisfy every assertion above while addressing "
			+ "no storage at all");
	}

	/// <summary>
	/// Resolves the destination identity a store advertises, failing if it does not advertise one.
	/// </summary>
	/// <typeparam name="TProjection">The read model the store holds.</typeparam>
	/// <param name="store">The store to interrogate.</param>
	/// <param name="origin">The derivation point that produced the store, named in the failure.</param>
	/// <returns>The advertised identity.</returns>
	/// <remarks>
	/// <b>The identity must not be the store itself.</b> The base projection-store interface answers
	/// <c>GetService</c> with a default implementation that returns the instance when it is already of the
	/// requested type, so a check that only tested for null would pass for a store that resolves no
	/// identity at all and happens to implement the interface directly. Requiring a distinct object is
	/// what makes this arm about the capability rather than about the cast.
	/// </remarks>
	private static IProjectionDestinationIdentity RequireIdentity<TProjection>(
		IProjectionStore<TProjection> store,
		string origin)
		where TProjection : class
	{
		var identity = store.GetService(typeof(IProjectionDestinationIdentity)) as IProjectionDestinationIdentity
			?? throw new ConformanceAssertionException(
				$"The store '{store.GetType().Name}' from {origin} does not provide "
				+ "IProjectionDestinationIdentity. Without it nothing can report where this read model is "
				+ "written, so a cutover cannot be planned and two read models configured against one "
				+ "destination cannot be refused at start-up.");

		Require(
			identity is not IProjectionStore<TProjection>,
			$"the identity from {origin} is the store itself, which is what the base interface's default "
			+ "GetService returns for a type the instance already satisfies. The capability must be a "
			+ "distinct value, or this arm passes for a store that resolves no identity");

		Require(
			!string.IsNullOrWhiteSpace(identity.Identity),
			$"the identity from {origin} is blank, so it names no destination");

		return identity;
	}

	/// <summary>Fails the arm when a required condition does not hold.</summary>
	/// <param name="condition">The condition that must hold.</param>
	/// <param name="because">What was expected, and what the store did instead.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when <paramref name="condition"/> is false.</exception>
	private static void Require(bool condition, string because)
	{
		if (!condition)
		{
			throw new ConformanceAssertionException(because);
		}
	}
}
