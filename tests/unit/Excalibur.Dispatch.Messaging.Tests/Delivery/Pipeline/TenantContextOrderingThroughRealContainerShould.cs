// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Pipeline;

/// <summary>
/// The tenant-ordering refusal must fire for a pipeline composed the way a real application composes
/// one, and it must fire without anyone resolving <see cref="IDispatchPipeline"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists beside the unit arms, and why those arms were not enough.</b> The sibling
/// suite constructs <c>new DispatchPipeline(...)</c> and hands it bare, undecorated middleware
/// instances. That is the one shape the real build path never produces. A middleware registered
/// <c>Scoped</c> reaches the pipeline as a per-dispatch stand-in that holds no instance;
/// <c>UseAt&lt;T&gt;(stage)</c> and <c>ForMessageKinds(...).Use&lt;T&gt;()</c> each wrap it as well.
/// None of those wrappers implements the capability markers.
/// </para>
/// <para>
/// So a rule that asked <c>entry is IEstablishesTenantContext</c> answered <b>false for a middleware
/// that does establish tenant context</b>, concluded there was no ordering relation to enforce, and
/// returned quietly. The framework's own establisher is registered <c>TryAddScoped</c>, so the refusal
/// was unreachable in essentially every real application — while the unit arms stayed green, because
/// their fixtures are never wrapped.
/// </para>
/// <para>
/// <b>These arms are RED without the type-level capability read</b>, which is the property the unit
/// arms cannot express: they bind the pipeline object, and the defect lives in what the composition
/// hands it. Both were RED before the fix with no mutation applied at all.
/// </para>
/// <para>
/// The second arm resolves <see cref="IDispatchMiddlewareInvoker"/> and never touches
/// <see cref="IDispatchPipeline"/>, because the invoker is what the dispatcher executes. A host that
/// resolves only the invoker previously got no verification whatsoever.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TenantContextOrderingThroughRealContainerShould
{
	/// <summary>
	/// SAFETY: a scoped establisher still carries its capability, so a reader ordered before it is
	/// refused.
	/// </summary>
	/// <remarks>
	/// The consumer registers the establisher <c>Scoped</c>, which is honoured, so the pipeline entry
	/// becomes a stand-in carrying no instance. An instance-level capability test cannot see through
	/// that; a type-level one can.
	/// </remarks>
	[Fact]
	public void RefuseAReaderOrderedBeforeAScopedEstablisher()
	{
		var provider = Compose(scopedEstablisher: true);

		var thrown = Should.Throw<Exception>(() => provider.GetRequiredService<IDispatchPipeline>());

		Explain(thrown).ShouldContain(
			"IRequiresTenantContext",
			Case.Sensitive,
			"the establisher is registered Scoped, so it reaches the pipeline as a per-dispatch stand-in. "
			+ "Reading the capability off that entry rather than off the type it stands for is what made "
			+ "this refusal unreachable in every host that has a service-scope factory.");
	}

	/// <summary>
	/// SAFETY, on the path the dispatcher actually uses: the refusal fires for a host that resolves only
	/// the invoker and never constructs a pipeline.
	/// </summary>
	/// <remarks>
	/// The check now runs where the resolved middleware list is published, so it covers both consumers.
	/// While it lived in <c>DispatchPipeline</c>'s constructor it ran only as a side effect of building
	/// an object this arm never asks for.
	/// </remarks>
	[Fact]
	public void RefuseAViolatingPipelineForAHostThatResolvesOnlyTheInvoker()
	{
		var provider = Compose(scopedEstablisher: true);

		var thrown = Should.Throw<Exception>(() => provider.GetRequiredService<IDispatchMiddlewareInvoker>());

		Explain(thrown).ShouldContain(
			"IEstablishesTenantContext",
			Case.Sensitive,
			"the dispatcher consults the invoker, never the configured IDispatchPipeline. A guarantee "
			+ "enforced only in the pipeline's constructor does not bind this path.");
	}

	/// <summary>
	/// LIVENESS: the correctly-ordered pipeline, with the same scoped registration and the same
	/// wrapping, composes and runs.
	/// </summary>
	/// <remarks>
	/// Without this, both arms above are satisfied by a rule that refuses every pipeline — the cheapest
	/// way never to admit a violation and the most expensive way to be wrong.
	/// </remarks>
	[Fact]
	public void AcceptTheSamePipelineWhenTheEstablisherComesFirst()
	{
		var provider = Compose(scopedEstablisher: true, readerFirst: false);

		_ = Should.NotThrow(() => provider.GetRequiredService<IDispatchMiddlewareInvoker>());
		_ = Should.NotThrow(() => provider.GetRequiredService<IDispatchPipeline>());
	}

	/// <summary>
	/// The undecorated case still works, so the type-level read did not trade one blind spot for
	/// another.
	/// </summary>
	[Fact]
	public void StillRefuseWhenTheEstablisherIsNotScopedAndSoIsNotWrapped()
	{
		var provider = Compose(scopedEstablisher: false);

		var thrown = Should.Throw<Exception>(() => provider.GetRequiredService<IDispatchMiddlewareInvoker>());

		Explain(thrown).ShouldContain("IRequiresTenantContext", Case.Sensitive);
	}

	/// <summary>
	/// Builds a real container through the production registration path.
	/// </summary>
	/// <param name="scopedEstablisher">
	/// When true the establisher is registered Scoped, which forces the per-dispatch stand-in wrapper.
	/// </param>
	/// <param name="readerFirst">
	/// When true the reader declares an earlier stage than the establisher, which is the violation.
	/// </param>
	/// <returns> The built provider. </returns>
	private static ServiceProvider Compose(bool scopedEstablisher, bool readerFirst = true)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (scopedEstablisher)
		{
			// A consumer's own Scoped registration is honoured, so the entry becomes a stand-in.
			_ = services.AddScoped<LateEstablisher>();
		}

		_ = readerFirst
			? services.AddDispatch(static dispatch => dispatch
				.UseMiddleware<EarlyReader>()
				.UseMiddleware<LateEstablisher>())
			: services.AddDispatch(static dispatch => dispatch
				.UseMiddleware<EarlyEstablisher>()
				.UseMiddleware<LateReader>());

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Flattens an exception chain, because a composition failure surfaces wrapped by the container.
	/// </summary>
	/// <param name="exception"> The thrown exception. </param>
	/// <returns> Every message in the chain, joined. </returns>
	private static string Explain(Exception exception)
	{
		var messages = new List<string>();
		for (Exception? current = exception; current is not null; current = current.InnerException)
		{
			messages.Add(current.Message);
		}

		return string.Join(" | ", messages);
	}

	private abstract class OrderedMiddleware(DispatchMiddlewareStage stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage { get; } = stage;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	private sealed class EarlyReader()
		: OrderedMiddleware(DispatchMiddlewareStage.Start), IRequiresTenantContext;

	private sealed class LateEstablisher()
		: OrderedMiddleware(DispatchMiddlewareStage.Authorization), IEstablishesTenantContext;

	private sealed class EarlyEstablisher()
		: OrderedMiddleware(DispatchMiddlewareStage.Start), IEstablishesTenantContext;

	private sealed class LateReader()
		: OrderedMiddleware(DispatchMiddlewareStage.Authorization), IRequiresTenantContext;
}
