// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Stub projection type for the ElasticSearch bridge conformance pin. No real
/// properties needed — the pin only asserts <c>IProjectionStore&lt;T&gt;</c> is registered
/// by the bridge path, not that it can connect to a live cluster.
/// </summary>
public sealed class ElasticSearchBridgeConformanceProjection { }

/// <summary>
/// Marker for S822 bd-x10iv0 A2 — <c>IEventSourcingBuilder.AddElasticSearchProjections</c>
/// bridge forwarding to
/// <c>IServiceCollection.AddElasticSearchProjections(nodeUri, configure)</c>.
/// See <see cref="ElasticSearchProjectionsEventSourcingBuilderExtensions"/>.
/// </summary>
public sealed class AddElasticSearchProjectionsBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 822 bd-x10iv0 (S792 Workstream A2 deferred from S793 D3):
/// the <c>IEventSourcingBuilder.AddElasticSearchProjections</c> bridge forwards to
/// <c>services.AddElasticSearchProjections(nodeUri, configure)</c>. With a minimal
/// registrar callback that registers one projection,
/// <see cref="IProjectionStore{T}"/> for the stub projection type is resolvable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A</b> — configure-callback bridge shape. Unlike the DataProcessing and CDC
/// bridges on <c>IExcaliburBuilder</c>, this bridge is on <c>IEventSourcingBuilder</c>
/// (composed via <c>AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; ...))</c>). With an
/// empty registrar callback, <b>nothing</b> is registered — the registrar only adds
/// services when <c>Add&lt;T&gt;()</c> is called. This pin provides a minimal callback
/// that registers one stub projection to exercise the full bridge forwarding path.
/// </para>
/// <para>
/// <b>IAsyncDisposable kit limitation:</b>
/// <see cref="Excalibur.Data.ElasticSearch.Projections.ElasticSearchProjectionStore{T}"/>
/// implements only <see cref="IAsyncDisposable"/> (no synchronous <see cref="IDisposable"/>).
/// The base <see cref="MinimalWiringConformanceTestKit{T}"/> uses synchronous
/// <c>using (var provider = ...)</c> in <see cref="MinimalWiringConformanceTestKit{T}.ExecuteIsolationGate"/>,
/// which throws at cleanup. <see cref="Gate_Isolation"/> is therefore implemented as a
/// custom async gate using <c>await using</c> instead of delegating to the base class.
/// The <see cref="MinimalWiringConformanceTestKit{T}.ExecuteIdempotenceGate"/> does not
/// resolve services (only counts descriptors) and works normally.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddElasticSearchProjectionsBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddElasticSearchProjectionsBridgeMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddEventSourcing(static es =>
			es.AddElasticSearchProjections("http://localhost:9200", static p =>
				p.Add<ElasticSearchBridgeConformanceProjection>())));

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IProjectionStore<ElasticSearchBridgeConformanceProjection>),
	};

	/// <inheritdoc />
	protected override bool ValidateOnBuild => false;

	/// <inheritdoc />
	protected override bool ValidateScopes => false;

	/// <summary>
	/// Bucket A isolation gate — bridge registers <c>IProjectionStore&lt;T&gt;</c> via TryAdd.
	/// </summary>
	/// <remarks>
	/// Custom async implementation because <c>ElasticSearchProjectionStore&lt;T&gt;</c> is
	/// <see cref="IAsyncDisposable"/>-only. The base
	/// <see cref="MinimalWiringConformanceTestKit{T}.ExecuteIsolationGate"/> uses synchronous
	/// <c>using</c> which throws <see cref="InvalidOperationException"/> on cleanup.
	/// This gate uses <c>await using</c> to correctly dispose the provider.
	/// </remarks>
	[Fact]
	public async Task Gate_Isolation()
	{
		var services = new ServiceCollection();
		services.AddLogging(static b => b.AddProvider(NullLoggerProvider.Instance));
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

		Invoke(services);

		await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = false,
			ValidateScopes = false,
		});

		foreach (var type in ExpectedResolvableServices)
		{
			var service = provider.GetService(type);
			service.ShouldNotBeNull(
				$"Minimal-Wiring contract Bucket A: {nameof(AddElasticSearchProjectionsBridgeMarker)} " +
				$"must leave {type.FullName} resolvable from an empty container, but GetService returned null.");
		}
	}

	/// <summary>Bucket A idempotence gate — second invocation is no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();
}
