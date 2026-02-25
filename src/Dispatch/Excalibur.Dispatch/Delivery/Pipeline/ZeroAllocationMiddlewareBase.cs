// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Base class for zero-allocation middleware implementations.
/// </summary>
public abstract class ZeroAllocationMiddlewareBase : IZeroAllocationMiddleware, IDispatchMiddleware
{
	/// <inheritdoc />
	public abstract DispatchMiddlewareStage Stage { get; }

	/// <inheritdoc />
	DispatchMiddlewareStage? IDispatchMiddleware.Stage => Stage;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public abstract ValueTask<(MiddlewareResult Result, MiddlewareContext Context)> ProcessAsync(
			MessageEnvelope<IDispatchMessage> envelope,
			MiddlewareContext context,
	CancellationToken cancellationToken);

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Zero-allocation middleware uses reflection-based helpers; trimming-safe usage requires explicit type preservation.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Zero-allocation middleware may require dynamic code; AOT users should disable these components.")]
	async ValueTask<IMessageResult> IDispatchMiddleware.InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
	DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken)
	{
		// Create envelope from message and context
		var metadata = MessageMetadata.FromContext(context);
		var recordMetadata = metadata.ToRecordMetadata();
		var envelope = new MessageEnvelope<IDispatchMessage>(message, recordMetadata, context);

		try
		{
			// Create a temporary middleware context (this allocates, but only for compatibility)
			var middlewareContext = new MiddlewareContext([]);

			var result = await ProcessAsync(envelope, middlewareContext, cancellationToken).ConfigureAwait(false);

			if (!result.Result.ContinueExecution)
			{
				return new BasicMessageResult(result.Result.Success, result.Result.Error);
			}

			// Continue with next middleware
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			envelope.Dispose();
		}
	}

	/// <summary>
	/// Simple message result implementation.
	/// </summary>
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
}
