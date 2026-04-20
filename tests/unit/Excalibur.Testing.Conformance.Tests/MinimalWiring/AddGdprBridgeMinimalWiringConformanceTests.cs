// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for S794 Workstream C1 — <c>IExcaliburBuilder.AddGdprErasure(...)</c> bridge
/// shipping in the new <c>Excalibur.Hosting.Compliance</c> package. See
/// <c>GdprExcaliburBuilderExtensions</c>.
/// </summary>
public sealed class AddGdprBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 794 Workstream C1 (<c>bd-it7p8w</c>, commit <c>9385b9de5</c>):
/// the <c>IExcaliburBuilder.AddGdprErasure</c> bridge forwards to
/// <c>Excalibur.Compliance.ErasureServiceCollectionExtensions.AddGdprErasure</c>
/// and inherits its Bucket-A contract (framework-defaulted
/// <see cref="IKeyManagementAdmin"/> + <see cref="IKeyManagementProvider"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Placement rationale (ADR-324 + COMPASS msg 1473 §3):</b> the bridge lives in the
/// separate <c>Excalibur.Hosting.Compliance</c> package (Option X) to avoid forcing every
/// <c>Excalibur.Hosting</c> consumer to take a transitive dep on the heavy compliance
/// stack (MongoDB.Driver, Npgsql, QuestPDF). The physical rename of
/// <c>Excalibur.Compliance</c> → <c>Excalibur.Compliance</c> per ADR-324
/// §Open-Questions §2 is deferred to S795.
/// </para>
/// <para>
/// Closes S792-A6 composition-root gap — GDPR can now configure through the single
/// <c>AddExcalibur(x =&gt; x.AddGdprErasure())</c> root. See also
/// <see cref="AddGdprErasureMinimalWiringConformanceTests"/> for the underlying extension's pin.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddGdprBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddGdprBridgeMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddGdprErasure());

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

		var admin = provider.GetRequiredService<IKeyManagementAdmin>();
		admin.ShouldNotBeNull();
		admin.GetType().Name.ShouldNotBe(nameof(InMemoryKeyManagementProvider));
	}

	/// <summary>Bucket A isolation gate — bridge forwards; underlying TryAdd default resolves.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — double-invocation no-op via TryAdd*.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer-registered IKeyManagementAdmin wins.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
