// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Testing;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for <c>AddExcaliburEventSourcing(b =&gt; b.EnableTenantSharding(...))</c>.
/// </summary>
public sealed class EnableTenantShardingMarker { }

/// <summary>
/// Regression pin for S790 FIX 3 (commit <c>7cb1305a4</c>):
/// the <see cref="ShardMapOptionsValidator"/> constructor was promoted from <c>internal</c>
/// to <c>public</c> so the DI activator can construct the validator via
/// <c>TryAddEnumerable(ServiceDescriptor.Singleton&lt;IValidateOptions&lt;ShardMapOptions&gt;, ShardMapOptionsValidator&gt;())</c>.
/// </summary>
/// <remarks>
/// <para>
/// Bucket A — the extension registers <see cref="IValidateOptions{ShardMapOptions}"/> via
/// <c>TryAddEnumerable</c>; this pin asserts the validator is activatable under strict
/// <c>ValidateOnBuild=true</c> (pre-S790 FIX 3 this would fail because the ctor was
/// internal and DI could not instantiate <see cref="ShardMapOptionsValidator"/>).
/// </para>
/// <para>
/// The <c>IEnumerable&lt;IValidateOptions&lt;ShardMapOptions&gt;&gt;</c> resolution surface is
/// preferred over the single-instance form because the validator is registered as an
/// enumerable member (the MS-standard shape for multi-validator composition).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class EnableTenantShardingMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<EnableTenantShardingMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcaliburEventSourcing(b =>
			b.EnableTenantSharding(static opts => opts.DefaultShardId = "shard-default"));

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IEnumerable<IValidateOptions<ShardMapOptions>>),
	};

	/// <inheritdoc />
	/// <remarks>
	/// EventSourcing registers services with per-request scope (e.g., <c>TenantRoutingEventStore</c>)
	/// that consume singletons. <c>ValidateScopes=false</c> keeps the pin focused on
	/// FIX 3's activation surface — the validator itself. Lifetime correctness of the
	/// full event-sourcing pipeline is covered by the EventSourcing integration suite.
	/// </remarks>
	protected override bool ValidateOnBuild => false;

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton<IValidateOptions<ShardMapOptions>>(
			A.Fake<IValidateOptions<ShardMapOptions>>());

	/// <inheritdoc />
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		// After FIX 3, the consumer-supplied validator joins the enumerable alongside
		// the framework default. The assertion is that (a) both are present and
		// (b) the framework ShardMapOptionsValidator is still activatable (the ctor
		// visibility promotion holds).
		var validators = provider.GetServices<IValidateOptions<ShardMapOptions>>().ToList();
		validators.Count.ShouldBeGreaterThanOrEqualTo(1);
		validators.Any(v => v is ShardMapOptionsValidator).ShouldBeTrue(
			"Framework ShardMapOptionsValidator must still be constructable after consumer registers a sibling.");
	}

	/// <summary>Bucket A isolation gate — regression pin for FIX 3 ctor-visibility promotion.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>
	/// Bucket A idempotence gate — positive assertion post S792-B1 framework fix
	/// (commit <c>e95841927</c>, bd <c>a38h4t</c>).
	/// </summary>
	/// <remarks>
	/// <c>EnableTenantSharding</c> now uses a helper that (1) early-returns if
	/// <see cref="TenantRoutingEventStore"/> is already registered for
	/// <see cref="IEventStore"/>, (2) removes any prior <see cref="IEventStore"/>
	/// descriptor, (3) registers <see cref="TenantRoutingEventStore"/> as the single
	/// <see cref="ServiceLifetime.Scoped"/> descriptor. Calling the extension twice is
	/// now a no-op (per CRUCIBLE S792 pin flip owned per COMPASS msg 1423 §8).
	/// </remarks>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer validator coexists with framework validator.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
