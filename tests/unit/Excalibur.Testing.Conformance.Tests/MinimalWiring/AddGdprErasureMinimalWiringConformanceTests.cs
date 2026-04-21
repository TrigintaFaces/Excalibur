// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Testing.Conformance.DependencyInjection;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for the <c>AddGdprErasure()</c> fluent-chain terminal in the minimal-wiring inventory.
/// </summary>
public sealed class AddGdprErasureMarker { }

/// <summary>
/// Regression pin for S790 FIX 2 (commit <c>963130a9a</c>):
/// <see cref="Microsoft.Extensions.DependencyInjection.ErasureServiceCollectionExtensions.AddGdprErasure(IServiceCollection, Action{ErasureOptions}?)"/>
/// must <c>TryAdd</c> a default <see cref="IKeyManagementAdmin"/> (the in-memory provider)
/// so a consumer who has not yet picked a KMS backend can still register the erasure layer
/// against an otherwise-empty <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Bucket A — the <c>IKeyManagementAdmin</c> default is the spec's canonical §5.1 exemplar.
/// <see cref="IErasureService"/> itself is registered as a factory closure that resolves
/// <see cref="IErasureStore"/> on first activation (consumer picks in-memory vs. SQL), so
/// <c>ValidateOnBuild=true</c> does not surface <c>IErasureStore</c> as a build-time gap.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddGdprErasureMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddGdprErasureMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddGdprErasure();

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IKeyManagementAdmin),
		typeof(IKeyManagementProvider),
	};

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton<IKeyManagementAdmin>(A.Fake<IKeyManagementAdmin>());

	/// <inheritdoc />
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		// The consumer-supplied admin must survive TryAdd in AddGdprErasure.
		var admin = provider.GetRequiredService<IKeyManagementAdmin>();
		admin.ShouldNotBeNull();
		// FakeItEasy proxies derive from a runtime-generated type whose declaring
		// type is not the production InMemoryKeyManagementProvider.
		admin.GetType().Name.ShouldNotBe(nameof(InMemoryKeyManagementProvider));
	}

	/// <summary>Minimal-Wiring isolation gate (Bucket A).</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Minimal-Wiring idempotence gate.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Minimal-Wiring override gate — consumer-registered <see cref="IKeyManagementAdmin"/> wins.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
