// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Pipeline;

/// <summary>
/// The pipeline must refuse a configuration in which tenant context is READ before it is ESTABLISHED.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists at all.</b> An earlier version of this constraint was written against the
/// concrete types <c>InboxMiddleware</c>, <c>TenantIdentityMiddleware</c> and <c>ThrottlingMiddleware</c>.
/// All three are <c>public sealed</c>, so a test could not produce a violating pipeline: the probe did
/// not fail, IT COULD NOT RUN. A constraint that cannot be violated in a test is decorative, and that
/// version was correctly reverted rather than landed.
/// </para>
/// <para>
/// The capability marker is what makes the constraint armable, and the arms below are the proof: each
/// one declares ITS OWN middleware implementing the marker, so the test never has to impersonate a
/// framework type. That is also the property a CONSUMER relies on — their tenant-reading middleware
/// gets the guarantee by implementing the marker, which a list of our type names could never give them.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TenantContextOrderingShould : UnitTestBase
{
	/// <summary>
	/// SAFETY: a reader ordered before the establisher is REFUSED.
	/// </summary>
	[Fact]
	public void RefuseAReaderOrderedBeforeTheEstablisher()
	{
		// Same stage, so the STABLE sort preserves registration order: the reader really does land first.
		var middleware = new IDispatchMiddleware[]
		{
			new TestTenantReader(DispatchMiddlewareStage.PreProcessing),
			new TestTenantEstablisher(DispatchMiddlewareStage.PreProcessing),
		};

		var ex = Should.Throw<InvalidOperationException>(() => new DispatchPipeline(middleware));

		ex.Message.ShouldContain(nameof(TestTenantReader));
		ex.Message.ShouldContain(nameof(TestTenantEstablisher));
	}

	/// <summary>
	/// SAFETY, the ordering that matters rather than the registration order: a reader in an EARLIER
	/// stage is refused even though it was registered second.
	/// </summary>
	/// <remarks>
	/// Without this arm, an implementation that merely compared registration indices would pass. The
	/// property is about EXECUTION order, and the stage is what decides that.
	/// </remarks>
	[Fact]
	public void RefuseAReaderWhoseStageRunsEarlierThanTheEstablisher()
	{
		var middleware = new IDispatchMiddleware[]
		{
			new TestTenantEstablisher(DispatchMiddlewareStage.Authorization),
			new TestTenantReader(DispatchMiddlewareStage.PreProcessing),
		};

		_ = Should.Throw<InvalidOperationException>(() => new DispatchPipeline(middleware));
	}

	/// <summary>
	/// LIVENESS: the correct order is ACCEPTED. Without this, "refuse everything" passes safety.
	/// </summary>
	[Fact]
	public void AcceptAReaderOrderedAfterTheEstablisher()
	{
		var middleware = new IDispatchMiddleware[]
		{
			new TestTenantEstablisher(DispatchMiddlewareStage.PreProcessing),
			new TestTenantReader(DispatchMiddlewareStage.Authorization),
		};

		_ = Should.NotThrow(() => new DispatchPipeline(middleware));
	}

	/// <summary>
	/// LIVENESS: a pipeline with no tenant markers at all is untouched by this rule.
	/// </summary>
	/// <remarks>
	/// This is the arm that keeps the check from being a tax on every other pipeline, and it is the
	/// shape of every pipeline we ship by default — nothing in the framework currently implements
	/// <see cref="IRequiresTenantContext"/>.
	/// </remarks>
	[Fact]
	public void AcceptAPipelineWithNoTenantMarkers()
	{
		var middleware = new IDispatchMiddleware[]
		{
			new TestUnmarked(DispatchMiddlewareStage.PreProcessing),
			new TestUnmarked(DispatchMiddlewareStage.Authorization),
		};

		_ = Should.NotThrow(() => new DispatchPipeline(middleware));
	}

	/// <summary>
	/// LIVENESS: a reader with NO establisher registered is accepted, because there is no ordering
	/// relation to violate.
	/// </summary>
	/// <remarks>
	/// Recorded as a deliberate decision rather than an oversight: whether a reader with no establisher
	/// should itself be refused is a SEPARATE question from ordering, and this change does not decide
	/// it. If it is ever ruled, this arm is the one that must flip.
	/// </remarks>
	[Fact]
	public void AcceptAReaderWhenNothingEstablishesTenantContext()
	{
		var middleware = new IDispatchMiddleware[] { new TestTenantReader(DispatchMiddlewareStage.PreProcessing) };

		_ = Should.NotThrow(() => new DispatchPipeline(middleware));
	}

	private abstract class MarkerMiddlewareBase(DispatchMiddlewareStage stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage { get; } = stage;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	private sealed class TestTenantReader(DispatchMiddlewareStage stage)
		: MarkerMiddlewareBase(stage), IRequiresTenantContext;

	private sealed class TestTenantEstablisher(DispatchMiddlewareStage stage)
		: MarkerMiddlewareBase(stage), IEstablishesTenantContext;

	private sealed class TestUnmarked(DispatchMiddlewareStage stage) : MarkerMiddlewareBase(stage);
}
