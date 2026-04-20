// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>AddExcaliburAudit</c> fluent-chain terminal in the
/// minimal-wiring inventory. See <c>management/specs/conformance-minimal-wiring-inventory.csv</c>.
/// </summary>
public sealed class AddExcaliburAuditMarker { }

/// <summary>
/// Regression pin for S790 FIX 5 (commit <c>2170aef25</c>):
/// <see cref="Microsoft.Extensions.DependencyInjection.AuditServiceCollectionExtensions.AddExcaliburAudit(IServiceCollection)"/>
/// is reclassified to <see cref="MinimalWiringBucket.ExplicitPrerequisite"/> (Bucket B)
/// per PM disposition msg 1352 + COMPASS msg 1353 (supersedes Bucket-C ruling in msg 1351).
/// </summary>
/// <remarks>
/// <para>
/// <b>Finding:</b> the S791 A2 harness caught the seventh instance of the S790 hidden-sibling
/// defect family on its first pin. <c>AuditMiddleware</c>'s ctor takes both
/// <see cref="IAuditMessagePublisher"/> AND <see cref="IOutboxDispatcher"/>;
/// <c>AddExcaliburAudit</c> registers neither.
/// </para>
/// <para>
/// <b>Sibling-shape decomposition</b> (ADR-322 §Decision-3 amendment):
/// <list type="bullet">
/// <item><description>
/// <see cref="IOutboxDispatcher"/> — framework-internal, no consumer choice.
/// Correct shape: Shape 1 / Bucket A <c>TryAddSingleton&lt;IOutboxDispatcher, ...&gt;()</c>
/// inside <c>AddExcaliburAudit</c>. <b>Framework fix pending S792 Beads.</b>
/// </description></item>
/// <item><description>
/// <see cref="IAuditMessagePublisher"/> — genuinely non-defaultable consumer choice
/// (Kafka / SNS / EventHubs / SIEM / in-memory dev stub). Correct shape: Shape 2 / Bucket B
/// <c>ValidateOnStart</c> with named-sibling error.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Bucket B test shape</b> (per spec §5.2 + PM admissibility ruling msg 1352):
/// <list type="bullet">
/// <item><description>
/// <see cref="Gate_Isolation"/> — extension invoked against unextended foundation must throw
/// an <see cref="InvalidOperationException"/> whose message contains
/// "<c>IAuditMessagePublisher</c>". Currently passes because .NET DI's
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceProviderOptions.ValidateOnBuild"/>
/// surfaces the missing sibling in its AggregateException. Once the framework fix lands,
/// the extension will emit a curated named-sibling error; the fragment match still holds.
/// </description></item>
/// <item><description>
/// <see cref="Gate_Override"/> — consumer pre-registers stub <see cref="IAuditMessagePublisher"/>
/// and <see cref="IOutboxDispatcher"/>; resolution is then green.
/// </description></item>
/// <item><description>
/// <see cref="Gate_Idempotence"/> — with stubs supplied, second call is a no-op.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Sprint scope:</b> per OVERWATCH msg 1349 + COMPASS msg 1353, the framework fix is
/// explicitly deferred to S792. S791 ships the harness + this pin as the documented
/// gap marker. This pin also subsumes <c>bd-k9mq6u</c> (A3 CQRS+Audit integration coverage)
/// per sprint plan A3.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddExcaliburAuditMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddExcaliburAuditMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcaliburAudit();

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.ExplicitPrerequisite;

	/// <inheritdoc />
	/// <remarks>
	/// Must contain the name of at least one genuinely non-defaultable sibling the extension
	/// advertises. Current .NET DI fails with an AggregateException whose inner-exception
	/// messages reference <c>IAuditMessagePublisher</c> by name; the forthcoming framework fix
	/// will emit a curated <c>"AddExcaliburAudit requires ..."</c> message that still contains
	/// this fragment. Either way the gate holds.
	/// </remarks>
	protected override string ExpectedPrerequisiteMessageFragment => nameof(IAuditMessagePublisher);

	/// <inheritdoc />
	/// <remarks>
	/// <b>Bucket B Override gate admissibility</b> (spec §5.1 per PM msg 1352): pre-register
	/// the non-defaultable sibling <see cref="IAuditMessagePublisher"/>. Post S792-B3
	/// (bd <c>drizep</c>, commit <c>00231a66d</c>), <see cref="IOutboxDispatcher"/> is framework-
	/// defaulted via <c>TryAddSingleton&lt;IOutboxDispatcher, DefaultOutboxDispatcher&gt;()</c>
	/// inside <c>AddExcaliburAudit</c> (Shape 1 / Bucket A), so the consumer no longer needs to
	/// supply it for DI activation. The pin asserts that once the consumer satisfies the
	/// registration-time-named <see cref="IAuditMessagePublisher"/> prerequisite, resolution
	/// is green without additional scaffolding.
	/// </remarks>
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IAuditMessagePublisher>());

	// S792-B2 (bd b5w27b, commit 00231a66d): AuditMiddleware lifetime corrected to Scoped
	// (TryAddEnumerable with ServiceLifetime.Scoped) matching IActivityContext. The captive-
	// dependency shape is resolved; ValidateScopes defaults to true and passes. Pin flip
	// (removal of the ValidateScopes=false override) owned by TestsDeveloper / CRUCIBLE.

	/// <summary>Bucket B isolation gate — empty foundation must fail with named-sibling error.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — with stubs pre-registered, second invocation is no-op.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket B override gate — with siblings stub-supplied, resolution green.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
