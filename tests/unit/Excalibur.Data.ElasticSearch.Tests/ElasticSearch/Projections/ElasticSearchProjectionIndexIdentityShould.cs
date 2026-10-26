// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Two read models that resolve to one Elasticsearch index overwrite each other on the same document
/// id, and nothing reports it. These arms bind the refusal that makes that unexpressible, and — the
/// half that is easy to forget — that every legitimate composition still starts.
/// </summary>
/// <remarks>
/// The index name defaults to the projection's SIMPLE type name, so two types with the same simple
/// name in different namespaces collide while their options buckets (keyed on the full name) do not.
/// That is the shape under test: the collision is in the resolved index string, never in the type.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
[Trait(TraitNames.Feature, TestFeatures.Projections)]
public sealed class ElasticSearchProjectionIndexIdentityShould
{
	private const string Node = "http://localhost:9200";

	/// <summary>
	/// SAFETY. Two distinct projection types taking the conventional default both resolve to
	/// "employeeprojection", so one silently overwrites the other's documents.
	/// </summary>
	/// <remarks>
	/// The exception TYPE is asserted, not merely that something threw: a missing node address would
	/// fail the options gate that runs first and would satisfy a bare "throws" assertion while this
	/// check was entirely absent.
	/// </remarks>
	[Fact]
	public void RefuseTwoProjectionsResolvingToOneIndex()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Ops.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		// Both owners are named, so the consumer can tell WHICH two read models collided without
		// bisecting their registrations.
		error.Message.ShouldContain(typeof(Hr.EmployeeProjection).FullName!);
		error.Message.ShouldContain(typeof(Ops.EmployeeProjection).FullName!);
		error.Message.ShouldContain("employeeprojection");
	}

	/// <summary>
	/// LIVENESS. The same two types, given the distinct index names the refusal asks for, start —
	/// and the container really resolves both stores.
	/// </summary>
	/// <remarks>
	/// Without this arm a guard that refused every registration would pass the safety arm above.
	/// </remarks>
	[Fact]
	public async Task AllowTwoSameNamedProjectionsGivenDistinctIndexNames()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.Index.IndexName = "hremployee")
				.Add<Ops.EmployeeProjection>(o => o.Index.IndexName = "opsemployee"));

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();

		// The store is IAsyncDisposable, so the scope must be disposed asynchronously.
		await using var scope = provider.CreateAsyncScope();
		scope.ServiceProvider.GetRequiredService<IProjectionStore<Hr.EmployeeProjection>>().ShouldNotBeNull();
		scope.ServiceProvider.GetRequiredService<IProjectionStore<Ops.EmployeeProjection>>().ShouldNotBeNull();
	}

	/// <summary>
	/// LIVENESS. The check is on the RESOLVED index string, so two same-named types separated only by
	/// their prefix are a legitimate composition and must start.
	/// </summary>
	[Fact]
	public void AllowSameNamedProjectionsSeparatedByPrefix()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.Index.IndexPrefix = "hr")
				.Add<Ops.EmployeeProjection>(o => o.Index.IndexPrefix = "ops"));

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

	/// <summary>
	/// LIVENESS. One projection on the conventional default is the common case and owes no
	/// configuration at all.
	/// </summary>
	[Fact]
	public void AllowOneProjectionTakingTheConventionalDefault()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections.Add<Hr.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

	/// <summary>
	/// LIVENESS. Registering one type twice is idempotent, not a collision — a shared registration
	/// helper called from two hosts in one process must not read as two owners of one index.
	/// </summary>
	[Fact]
	public void AllowTheSameProjectionRegisteredTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Hr.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

	/// <summary>
	/// SAFETY, on the OTHER reachable path. A host does not call <c>ValidateStartupGates()</c> — it
	/// starts hosted services — so the check must fire there too.
	/// </summary>
	/// <remarks>
	/// The two paths are registered separately, so a check wired to only one of them is inert for
	/// every consumer who uses the other. This arm goes RED if the hosted registration is dropped
	/// while every type involved stays present and the host-less arm stays green.
	/// </remarks>
	[Fact]
	public async Task RefuseACollisionWhenAHostStartsItsServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Ops.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			foreach (var hosted in provider.GetServices<IHostedService>())
			{
				await hosted.StartAsync(CancellationToken.None);
			}
		});

		error.Message.ShouldContain(typeof(Hr.EmployeeProjection).FullName!);
		error.Message.ShouldContain(typeof(Ops.EmployeeProjection).FullName!);
	}

	/// <summary>
	/// LIVENESS, on the hosted path. A legitimate composition must still start its hosted services.
	/// </summary>
	[Fact]
	public async Task StartHostedServicesForADistinctlyNamedComposition()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.Index.IndexName = "hremployee")
				.Add<Ops.EmployeeProjection>(o => o.Index.IndexName = "opsemployee"));

		var provider = services.BuildServiceProvider();

		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
			await hosted.StopAsync(CancellationToken.None);
		}
	}

	/// <summary>
	/// LIVENESS. One index name on TWO clusters is not a collision — nothing can overwrite anything,
	/// because the two stores never address the same Elasticsearch.
	/// </summary>
	/// <remarks>
	/// This is the false-alarm case. Keying the check on the index name alone would refuse a perfectly
	/// sound multi-cluster composition at start-up, which is a worse outcome than the defect for the
	/// consumer who hits it: their deployment is correct and the framework will not start it.
	/// </remarks>
	[Fact]
	public void AllowOneIndexNameOnTwoDifferentClusters()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.NodeUri = "http://cluster-a:9200")
				.Add<Ops.EmployeeProjection>(o => o.NodeUri = "http://cluster-b:9200"));

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

	/// <summary>
	/// SAFETY, the converse of the arm above. The same two read models on ONE cluster still collide,
	/// so the cluster is a discriminator and not an escape hatch.
	/// </summary>
	[Fact]
	public void RefuseOneIndexNameOnOneClusterReachedUnderDifferentCredentials()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.Auth.Username = "hr")
				.Add<Ops.EmployeeProjection>(o => o.Auth.Username = "ops"));

		var provider = services.BuildServiceProvider();

		// Different credentials, one cluster, one index: the documents still overwrite each other, so
		// the credential must NOT separate them.
		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		error.Message.ShouldContain(typeof(Hr.EmployeeProjection).FullName!);
		error.Message.ShouldContain(typeof(Ops.EmployeeProjection).FullName!);
	}

	/// <summary>
	/// SAFETY. One cluster SPELLED two ways is one cluster. A trailing slash on one override must not
	/// buy an exemption.
	/// </summary>
	/// <remarks>
	/// This is the silent direction and it is the one that matters: a raw string comparison reads these
	/// as two clusters, finds no collision, and lets two read models overwrite each other in
	/// 'projections-employeeprojection' with nothing reported. NodeUri is a plain string that nothing
	/// rewrites, so the spelling is the consumer's and the folding has to be ours.
	/// </remarks>
	[Theory]
	[InlineData("http://localhost:9200/")]          // a trailing slash
	[InlineData("http://LOCALHOST:9200")]           // host case
	[InlineData("  http://localhost:9200  ")]       // surrounding whitespace
	public void RefuseACollisionWhenOneClusterIsSpelledTwoWays(string otherSpelling)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.NodeUri = Node)
				.Add<Ops.EmployeeProjection>(o => o.NodeUri = otherSpelling));

		var provider = services.BuildServiceProvider();

		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		error.Message.ShouldContain(typeof(Hr.EmployeeProjection).FullName!);
		error.Message.ShouldContain(typeof(Ops.EmployeeProjection).FullName!);
	}

	/// <summary>
	/// SAFETY. The same address supplied through NodeUris on one projection and NodeUri on the other is
	/// still one cluster -- both entry points are ours, so a host can legitimately mix them.
	/// </summary>
	[Fact]
	public void RefuseACollisionAcrossTheNodeUriAndNodeUrisForms()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.NodeUri = Node)
				.Add<Ops.EmployeeProjection>(o => o.NodeUris = [new Uri(Node)]));

		var provider = services.BuildServiceProvider();

		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		error.Message.ShouldContain(typeof(Ops.EmployeeProjection).FullName!);
	}

	/// <summary>
	/// LIVENESS, guarding the arm above from over-folding. A different PATH on the same host is a
	/// different node -- a reverse proxy distinguishes clusters that way -- so it must still be allowed.
	/// </summary>
	[Fact]
	public void AllowTwoClustersDistinguishedOnlyByBasePath()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>(o => o.NodeUri = "http://proxy:9200/hr")
				.Add<Ops.EmployeeProjection>(o => o.NodeUri = "http://proxy:9200/ops"));

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

	/// <summary>
	/// THE PREDICATE ARM. The check must key on the index the STORE actually writes to, not on its own
	/// arithmetic — and the collision it reports must be a real one.
	/// </summary>
	/// <remarks>
	/// Every other arm asserts the validator's verdict, so all of them stay green if the validator and
	/// the store ever derive the index differently. This one resolves both real stores and reads the
	/// index each one resolved for itself: they must be EQUAL (the collision is real, not an artifact of
	/// how the check counts) and the reported message must name that exact string.
	/// </remarks>
	[Fact]
	public async Task KeyOnTheIndexTheStoreItselfResolves()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Ops.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		// The gate is a START-UP check, not a registration failure, so the stores still resolve here.
		await using var scope = provider.CreateAsyncScope();
		var hr = (ElasticSearchProjectionStore<Hr.EmployeeProjection>)scope.ServiceProvider
			.GetRequiredService<IProjectionStore<Hr.EmployeeProjection>>();
		var ops = (ElasticSearchProjectionStore<Ops.EmployeeProjection>)scope.ServiceProvider
			.GetRequiredService<IProjectionStore<Ops.EmployeeProjection>>();

		// The collision is real: both stores resolved the SAME destination index.
		ops.ResolvedIndexName.ShouldBe(hr.ResolvedIndexName);

		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		// And the refusal names the string the stores actually use.
		error.Message.ShouldContain(hr.ResolvedIndexName);
	}

	/// <summary>
	/// The refusal must carry the two things a consumer cannot work out for themselves: a line they can
	/// paste, and the fact that an index already in use now holds both read models' documents.
	/// </summary>
	[Fact]
	public void TellTheConsumerWhatToTypeAndThatTheExistingIndexNeedsRebuilding()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Ops.EmployeeProjection>());

		var provider = services.BuildServiceProvider();

		var error = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		error.Message.ShouldContain("o.Index.IndexName");
		error.Message.ShouldContain("PER PROJECTION");
		error.Message.ShouldContain("rebuild");
	}

	/// <summary>Read models whose simple names differ never collide.</summary>
	[Fact]
	public void AllowProjectionsWithDistinctSimpleNames()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjections(
			shared => shared.NodeUri = Node,
			projections => projections
				.Add<Hr.EmployeeProjection>()
				.Add<Hr.CustomerProjection>());

		var provider = services.BuildServiceProvider();

		_ = provider.ValidateStartupGates();
	}

}
