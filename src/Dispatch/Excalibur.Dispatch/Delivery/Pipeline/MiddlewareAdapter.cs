// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Adapter to use existing IDispatchMiddleware in the zero-allocation pipeline.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MiddlewareAdapter" /> class. </remarks>
public sealed class MiddlewareAdapter(IDispatchMiddleware middleware) : IZeroAllocationMiddleware, IDisposable
{
	private readonly IDispatchMiddleware _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));
	private readonly ThreadLocal<DispatchRequestDelegate?> _cachedDelegate = new(static () => null);

	/// <inheritdoc />
	public DispatchMiddlewareStage Stage => _middleware.Stage ?? DispatchMiddlewareStage.End;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public async ValueTask<(MiddlewareResult Result, MiddlewareContext Context)> ProcessAsync(
		MessageEnvelope<IDispatchMessage> envelope,
		MiddlewareContext context,
		CancellationToken cancellationToken)
	{
		// Get or create cached delegate to avoid allocation
		var nextDelegate = GetOrCreateDelegate(context);

		// Execute the middleware
		var result = await _middleware.InvokeAsync(
			envelope.Message,
			envelope.Context,
			nextDelegate,
			cancellationToken).ConfigureAwait(false);

		// Convert result
		if (!result.Succeeded)
		{
			return (MiddlewareResult.StopWithError(result.ErrorMessage ?? "Unknown error"), context);
		}

		// Check if we should continue (based on context state)
		return (context.HasNext ? MiddlewareResult.Continue() : MiddlewareResult.StopWithSuccess(), context);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="MiddlewareAdapter" />.
	/// </summary>
	public void Dispose() => _cachedDelegate?.Dispose();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "Lambda parameters required by DispatchRequestDelegate signature; context parameter reserved for future caching optimization")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Context parameter reserved for future delegate caching optimization based on context state")]
	private DispatchRequestDelegate GetOrCreateDelegate(MiddlewareContext context)
	{
		// Try to get cached delegate
		var cached = _cachedDelegate.Value;
		if (cached != null)
		{
			return cached;
		}

		// Create new delegate (this allocates, but only once per thread)
		DispatchRequestDelegate newDelegate = static (message, messageContext, ct) =>
			new ValueTask<IMessageResult>(new BasicMessageResult(success: true, error: null));

		_cachedDelegate.Value = newDelegate;
		return newDelegate;
	}

	private sealed class BasicMessageResult(bool success, string? error) : IMessageResult
	{
		public bool Succeeded { get; } = success;

		public IMessageProblemDetails? ProblemDetails { get; } = error != null
			? new SimpleProblemDetails(error)
			: null;

		public RoutingDecision? RoutingDecision { get; }

		public IValidationResult? ValidationResult { get; }

		public IAuthorizationResult? AuthorizationResult { get; }

		public bool CacheHit { get; }

		public string? ErrorMessage => ProblemDetails?.Detail;

		/// <summary>
		/// Legacy properties.
		/// </summary>
		public bool Success { get; } = success;

		public string? Error { get; } = error;

		/// <summary>
		/// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions.
		/// </summary>
		object? IMessageResult.ValidationResult => ValidationResult;

		object? IMessageResult.AuthorizationResult => AuthorizationResult;
	}

	private sealed class SimpleProblemDetails(string error) : IMessageProblemDetails
	{
		public string Type { get; set; } = "about:blank";

		public string Title { get; set; } = "Error";

		public int ErrorCode { get; set; } = 500;

		public string Detail { get; set; } = error;

		public string Instance { get; set; } = string.Empty;

		public IDictionary<string, object?> Extensions { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
	}
}
