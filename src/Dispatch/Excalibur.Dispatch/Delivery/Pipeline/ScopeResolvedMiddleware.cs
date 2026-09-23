// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Carries the middleware type a pipeline entry stands for, when the entry is not the middleware
/// itself. Anything that reasons about middleware identity by runtime type must read this instead: a
/// per-dispatch resolver shares its runtime type with every other resolver in the process.
/// </summary>
internal interface IMiddlewareTypeIdentity
{
	/// <summary>
	/// Gets the type of the middleware this entry stands for.
	/// </summary>
	Type MiddlewareType { get; }
}

/// <summary>
/// A pipeline entry for a middleware the container will not serve from one shared instance -- one
/// registered <see cref="ServiceLifetime.Scoped"/>, or one whose constructor graph reaches a scoped
/// service. It holds no middleware instance; it resolves one per dispatch, from the scope that
/// dispatch belongs to.
/// </summary>
/// <remarks>
/// <para>
/// The composed pipeline is a singleton, so resolving a scoped middleware while composing it captures
/// that middleware -- and everything it was constructed with -- for the life of the process. Under
/// <see cref="ServiceProviderOptions.ValidateScopes"/> the container refuses the resolution outright;
/// with scope validation off it succeeds and one middleware graph silently serves every request. The
/// composition can be a singleton; the instances cannot.
/// </para>
/// <para>
/// Scope selection is delegated to <see cref="HandlerScopeResolver"/>, the same precedence a scoped
/// handler already uses: the dispatch context's own request scope, else the ambient scope an
/// <see cref="IDispatchAmbientScopeAccessor"/> surfaces, else a scope created for this dispatch and
/// disposed once it completes. A created scope is published on
/// <see cref="IMessageContext.RequestServices"/> for the duration, so the middleware nested inside this
/// one -- and the handler at the bottom -- resolve from the same scope rather than each making their
/// own. The previous value is restored afterwards.
/// </para>
/// <para>
/// This is the model ASP.NET Core uses for <c>IMiddleware</c>: the pipeline is built once, and each
/// request resolves its middleware from the request's own <see cref="IServiceProvider"/>.
/// </para>
/// <para>
/// <see cref="IDispatchMiddleware.Stage"/> and <see cref="IDispatchMiddleware.ApplicableMessageKinds"/>
/// describe the middleware type rather than any one instance, and ordering and applicability filtering
/// both need them before a dispatch exists. They are read once, from the instance the pipeline build
/// resolved inside its own scope, and carried here. Nothing else of that instance is retained.
/// </para>
/// </remarks>
internal sealed class ScopeResolvedMiddleware : IDispatchMiddleware, IMiddlewareTypeIdentity
{
	private readonly Func<IServiceProvider, IDispatchMiddleware?> _factory;
	private readonly HandlerScopeResolver _scopeResolver;
	private readonly IServiceProvider _root;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScopeResolvedMiddleware"/> class.
	/// </summary>
	/// <param name="middlewareType"> The middleware type this entry stands for. </param>
	/// <param name="factory"> Resolves the middleware from a provider. </param>
	/// <param name="stage"> The stage read from the build-time probe. </param>
	/// <param name="applicableMessageKinds"> The applicable kinds read from the build-time probe. </param>
	/// <param name="scopeResolver"> Supplies the scope each dispatch resolves in. </param>
	/// <param name="root"> The root provider, used only to recognise a context whose request services are the root. </param>
	public ScopeResolvedMiddleware(
		Type middlewareType,
		Func<IServiceProvider, IDispatchMiddleware?> factory,
		DispatchMiddlewareStage? stage,
		MessageKinds applicableMessageKinds,
		HandlerScopeResolver scopeResolver,
		IServiceProvider root)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(scopeResolver);
		ArgumentNullException.ThrowIfNull(root);

		MiddlewareType = middlewareType;
		_factory = factory;
		Stage = stage;
		ApplicableMessageKinds = applicableMessageKinds;
		_scopeResolver = scopeResolver;
		_root = root;
	}

	/// <inheritdoc />
	public Type MiddlewareType { get; }

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage { get; }

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds { get; }

	/// <inheritdoc />
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		return _scopeResolver.RunAsync(
			MiddlewareType,
			PreferredScope(context),
			new InvocationState(this, message, context, nextDelegate, cancellationToken),
			// static: a capturing lambda would allocate a closure and a delegate on every dispatch, and
			// every scoped middleware on every dispatch funnels through here.
			static async (scopedProvider, state) =>
			{
				// Rebind the context to the resolved scope for the duration, exactly as the scoped
				// handler path does. When the scope was created here rather than borrowed, this is what
				// makes the rest of the pipeline and the handler share it instead of each creating one.
				var previousServices = state.Context.RequestServices;
				state.Context.RequestServices = scopedProvider;

				try
				{
					var middleware = state.Owner._factory(scopedProvider);
					if (middleware is null)
					{
						// The same fail-open the pipeline build applies to an Optional entry it cannot
						// materialize: the stage does not run, and the dispatch continues.
						return await state.NextDelegate(state.Message, state.Context, state.CancellationToken)
							.ConfigureAwait(false);
					}

					return await middleware
						.InvokeAsync(state.Message, state.Context, state.NextDelegate, state.CancellationToken)
						.ConfigureAwait(false);
				}
				finally
				{
					// Only a value the context can accept back. A context that never carried request
					// services reads as null here, and an implementation is entitled to reject null on the
					// setter -- throwing from this finally would replace the dispatch result with an
					// exception about bookkeeping.
					if (previousServices is not null && !ReferenceEquals(previousServices, scopedProvider))
					{
						state.Context.RequestServices = previousServices;
					}
				}
			});
	}

	/// <summary>
	/// Returns the request scope the caller already carries, or <see langword="null"/> so the resolver
	/// falls back to the ambient scope or creates one.
	/// </summary>
	/// <remarks>
	/// The same two disqualifications the scoped handler path applies, and for the same reason: the
	/// provider a consumer gets back from <c>BuildServiceProvider()</c> is not an
	/// <see cref="IServiceScope"/>, while a real scope's provider is, so a context constructed from the
	/// root is rejected rather than treated as a request scope.
	/// </remarks>
	private IServiceProvider? PreferredScope(IMessageContext context)
	{
		var requestServices = context.RequestServices;

		return requestServices is not null
			&& !ReferenceEquals(requestServices, _root)
			&& requestServices is IServiceScope
				? requestServices
				: null;
	}

	private readonly struct InvocationState(
		ScopeResolvedMiddleware owner,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		public ScopeResolvedMiddleware Owner { get; } = owner;

		public IDispatchMessage Message { get; } = message;

		public IMessageContext Context { get; } = context;

		public DispatchRequestDelegate NextDelegate { get; } = nextDelegate;

		public CancellationToken CancellationToken { get; } = cancellationToken;
	}
}
