// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;

using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

namespace Excalibur.Data.Tests.ProjectionVersioning
{
	/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
	public sealed class UnversionedOrderView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The same read model, labelled as the generation it already is.</summary>
	[ProjectionVersion(1)]
	public sealed class FirstGenerationOrderView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The next generation of that read model.</summary>
	[ProjectionVersion(2)]
	public sealed class SecondGenerationOrderView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.Firestore.Projections
{
	using FirstGenerationOrderView =
		global::Excalibur.Data.Tests.ProjectionVersioning.FirstGenerationOrderView;
	using SecondGenerationOrderView =
		global::Excalibur.Data.Tests.ProjectionVersioning.SecondGenerationOrderView;
	using UnversionedOrderView =
		global::Excalibur.Data.Tests.ProjectionVersioning.UnversionedOrderView;

	/// <summary>
	/// Binds a declared projection generation to the Firestore SUBCOLLECTION a store addresses, and to the
	/// destination identity it advertises.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The generation lands on the projection segment -- the document whose "items" subcollection holds
	/// the read model -- so a bump addresses a subcollection of its own inside the collection already
	/// configured, rather than renaming a collection the consumer named.
	/// </para>
	/// <para>
	/// The segment is produced by a document-id transform (path escaping, reserved-shape prefixing,
	/// byte-wise truncation). These arms assert the generation survives it VERBATIM rather than assuming
	/// it does: the hyphen and the digits must appear in the resolved path unchanged.
	/// </para>
	/// <para>
	/// No server is contacted. A resource path is pure string composition over the project and database
	/// ids, and the store's identity performs no RPC.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionVersionDestinationShould
	{
		private const string Project = "versioning-project";

		private const string Collection = "projections";

		// The segment and identity a Firestore projection resolves to with this configuration and no
		// declared generation. HARD-CODED ON PURPOSE: deriving these through the convention under test
		// would make the arm agree with whatever that convention currently does, which is precisely the
		// drift it exists to catch.
		private const string UnversionedSegment =
			"Excalibur.Data.Tests.ProjectionVersioning.UnversionedOrderView";

		private const string UnversionedIdentity =
			"firestore/projects/versioning-project/databases/(default)/documents/projections/"
			+ "Excalibur.Data.Tests.ProjectionVersioning.UnversionedOrderView/items";

		/// <summary>
		/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
		/// subcollection it addresses today, and so does one declaring the first. Without it, a design
		/// that versions everything satisfies every safety arm below while silently re-folding every
		/// existing read model from zero into a subcollection nothing has written.
		/// </summary>
		[Fact]
		public async Task ResolveTheUnchangedSegmentAndIdentityForAProjectionDeclaringNoGenerationOrTheFirst()
		{
			FirestoreProjectionCollectionConvention
				.GetProjectionSegment<UnversionedOrderView>()
				.ShouldBe(UnversionedSegment);

			(await ResolveIdentityAsync<UnversionedOrderView>()).ShouldBe(UnversionedIdentity);

			FirestoreProjectionCollectionConvention
				.GetProjectionSegment<FirstGenerationOrderView>()
				.ShouldBe("Excalibur.Data.Tests.ProjectionVersioning.FirstGenerationOrderView");
			(await ResolveIdentityAsync<FirstGenerationOrderView>()).ShouldBe(
				"firestore/projects/versioning-project/databases/(default)/documents/projections/"
				+ "Excalibur.Data.Tests.ProjectionVersioning.FirstGenerationOrderView/items");
		}

		/// <summary>
		/// SAFETY. A bump moves the destination: a new subcollection, and therefore a new identity. The
		/// generation segment survives the document-id transform verbatim.
		/// </summary>
		[Fact]
		public async Task ResolveADifferentSegmentAndIdentityWhenTheDeclaredGenerationIsBumped()
		{
			var segment = FirestoreProjectionCollectionConvention.GetProjectionSegment<SecondGenerationOrderView>();

			// Asserted as a literal, so a transform that mangled, escaped or dropped the generation would
			// go red here rather than being hidden by an identity comparison that only needs them to
			// differ.
			segment.ShouldBe("Excalibur.Data.Tests.ProjectionVersioning.SecondGenerationOrderView-v2");

			var identity = await ResolveIdentityAsync<SecondGenerationOrderView>();
			identity.ShouldBe(
				"firestore/projects/versioning-project/databases/(default)/documents/projections/"
				+ "Excalibur.Data.Tests.ProjectionVersioning.SecondGenerationOrderView-v2/items");
			identity.ShouldNotBe(
				UnversionedIdentity,
				"a declared bump that resolved the same subcollection would fold the new shape into the "
					+ "documents already serving reads");

			// THE CONFIGURED COLLECTION IS NOT VERSIONED. The consumer named it; the generation addresses
			// a subcollection inside it.
			identity.ShouldContain("/documents/" + Collection + "/");
		}

		/// <summary>
		/// SAFETY. Two generations of one read model, in ONE configured collection and registered in ONE
		/// service collection, are two destinations resolvable at once -- so the previous generation keeps
		/// its documents and keeps serving reads while the next folds.
		/// </summary>
		[Fact]
		public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(CreateDb());
			services.AddFirestoreProjectionStore<FirstGenerationOrderView>(o => o.CollectionName = Collection);
			services.AddFirestoreProjectionStore<SecondGenerationOrderView>(o => o.CollectionName = Collection);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var first = Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<FirstGenerationOrderView>>());
			var second = Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<SecondGenerationOrderView>>());

			first.ShouldEndWith("/Excalibur.Data.Tests.ProjectionVersioning.FirstGenerationOrderView/items");
			second.ShouldEndWith("/Excalibur.Data.Tests.ProjectionVersioning.SecondGenerationOrderView-v2/items");
			first.ShouldNotBe(second);
		}

		private static async Task<string> ResolveIdentityAsync<TProjection>()
			where TProjection : class
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(CreateDb());
			services.AddFirestoreProjectionStore<TProjection>(options => options.CollectionName = Collection);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			return Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>());
		}

		private static string Identity<TProjection>(IProjectionStore<TProjection> store)
			where TProjection : class
		{
			var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

			// The capability must be a distinct object rather than the store answering through the default
			// interface implementation, which hands back `this` for any assignable type. Without this the
			// arm would pass for a store that resolves no identity at all.
			(identity is IProjectionStore<TProjection>).ShouldBeFalse(
				"the identity must come from the store's own GetService, not the default interface implementation");

			return identity.Identity;
		}

		/// <summary>
		/// Builds a client against a faked gRPC stub. No server is contacted.
		/// </summary>
		private static FirestoreDb CreateDb() => FirestoreDb.Create(Project, A.Fake<FirestoreClient>());
	}
}
