// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Configuration;

/// <summary>Registers middleware with explicit metadata without activating a composition-time probe.</summary>
public static class PipelineBuilderDeferredExtensions
{
	/// <summary>Adds required middleware that is activated only when an applicable message executes.</summary>
	/// <typeparam name="TMiddleware">The exact concrete middleware type returned by the factory.</typeparam>
	/// <param name="builder">The pipeline builder.</param>
	/// <param name="middlewareFactory">Resolves DI-owned middleware from the dispatch service provider.</param>
	/// <param name="stage">The authoritative pipeline stage.</param>
	/// <param name="messageKinds">The authoritative applicable message kinds.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// Use this path for async-only disposable middleware or dependencies. The stock pipeline builder
	/// supplies a dispatch scope and awaits disposal of scopes it owns. Borrowed scopes remain caller-owned.
	/// The factory must return the exact declared type; null or a derived type fails before invocation.
	/// Factory activation is validated at dispatch time, not startup. Custom pipeline builders must supply
	/// a valid <see cref="IMessageContext.RequestServices"/>. Instances created outside DI remain the
	/// factory owner's disposal responsibility. Stage and kinds supplied here override instance metadata.
	/// </remarks>
	public static IPipelineBuilder UseDeferred<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
		this IPipelineBuilder builder,
		Func<IServiceProvider, TMiddleware> middlewareFactory,
		DispatchMiddlewareStage? stage,
		MessageKinds messageKinds)
		where TMiddleware : class, IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(middlewareFactory);
		if (typeof(TMiddleware).IsAbstract || typeof(TMiddleware).IsInterface)
		{
			throw new ArgumentException("Deferred middleware requires an exact concrete middleware type.", nameof(middlewareFactory));
		}
		var metadata = new DeferredMiddleware<TMiddleware>(middlewareFactory, stage, messageKinds);
		return builder.Use(_ => metadata);
	}

	private sealed class DeferredMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory,
		DispatchMiddlewareStage? stage, MessageKinds messageKinds) : IDispatchMiddleware, IMiddlewareTypeIdentity
		where TMiddleware : class, IDispatchMiddleware
	{
		public Type MiddlewareType => typeof(TMiddleware);
		public DispatchMiddlewareStage? Stage => stage;
		public MessageKinds ApplicableMessageKinds => messageKinds;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(context);
			var middleware = factory(context.RequestServices);
			if (middleware is null || middleware.GetType() != typeof(TMiddleware))
			{
				throw new InvalidOperationException($"Deferred middleware factory must return exactly '{typeof(TMiddleware).FullName}'.");
			}
			return middleware.InvokeAsync(message, context, nextDelegate, cancellationToken);
		}
	}
}
