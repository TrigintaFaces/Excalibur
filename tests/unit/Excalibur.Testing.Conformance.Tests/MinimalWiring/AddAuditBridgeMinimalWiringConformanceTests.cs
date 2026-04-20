// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for S792 Workstream A5 — <c>IExcaliburBuilder.AddAudit()</c> bridge forwarding
/// to <c>AddExcaliburAudit</c>. See <c>AuditExcaliburBuilderExtensions</c>.
/// </summary>
public sealed class AddAuditBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 792 Workstream A5 (<c>bd-c5dryp</c>, commit <c>d8f4cafe8</c>):
/// the <c>IExcaliburBuilder.AddAudit</c> bridge forwards to <c>AddExcaliburAudit</c> and
/// inherits its Bucket-B contract (non-defaultable <see cref="IAuditMessagePublisher"/>).
/// </summary>
/// <remarks>
/// <para>
/// Bucket B — <see cref="IAuditMessagePublisher"/> is a consumer-choice sibling (Kafka /
/// SNS / EventHubs / SIEM / in-memory stub); framework-supplied default is deliberately
/// absent per ADR-322 Shape-2. The bridge does not alter this contract — invoking
/// <c>AddExcalibur(x =&gt; x.AddAudit())</c> against an empty foundation must still surface
/// the missing sibling with a message containing the fragment
/// <c>IAuditMessagePublisher</c>.
/// </para>
/// <para>
/// Filed as <c>bd-elyhe9</c> S793 follow-up. See also
/// <see cref="AddExcaliburAuditMinimalWiringConformanceTests"/> for the underlying extension's pin.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddAuditBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddAuditBridgeMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddAudit());

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.ExplicitPrerequisite;

	/// <inheritdoc />
	protected override string ExpectedPrerequisiteMessageFragment => nameof(IAuditMessagePublisher);

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IAuditMessagePublisher>());

	/// <summary>Bucket B isolation gate — bridge surfaces the underlying Bucket B prerequisite.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — with stub supplied, second invocation is no-op.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket B override gate — with IAuditMessagePublisher supplied, resolution is green.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
