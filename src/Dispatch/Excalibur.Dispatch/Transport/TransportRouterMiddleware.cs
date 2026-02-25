// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Middleware that validates transport messages and ensures message kinds are accepted by the binding.
/// </summary>
/// <remarks>
/// <para>
/// This middleware validates that messages received via transport adapters match the binding's accepted message kinds. Transport binding
/// resolution is handled by the <see cref="ITransportContextProvider" /> in the Dispatcher BEFORE middleware invocation.
/// </para>
/// <para>
/// The binding is retrieved from the context where it was stored by the Dispatcher during transport context resolution. Pipeline profile
/// selection also happens in the Dispatcher, before the middleware chain is constructed.
/// </para>
/// </remarks>
public sealed partial class TransportRouterMiddleware(ILogger<TransportRouterMiddleware> logger) : IDispatchMiddleware
{
	private readonly ILogger<TransportRouterMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Routing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// The transport binding is resolved by <see cref="ITransportContextProvider" /> in the Dispatcher BEFORE middleware invocation. This
	/// middleware retrieves the binding from context and validates that the message kind is accepted.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"DetermineMessageKind uses reflection to check message interfaces, but this is acceptable in transport routing middleware as it operates on message types that are registered at startup and preserved through DI. Message types in messaging systems are known at compile time and registered explicitly.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Get transport binding from context (set by Dispatcher via ITransportContextProvider) If no binding exists, this is a direct
		// dispatch - continue normally
		var binding = context.TransportBinding();
		if (binding == null)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Validate that message kind is accepted by the binding
		var messageKind = DetermineMessageKind(message);
		if ((binding.AcceptedMessageKinds & messageKind) == MessageKinds.None)
		{
			LogMessageKindNotAccepted(messageKind, binding.Name);

			var problemDetails = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.Routing,
				Title = "Message Kind Not Accepted",
				Status = 400,
				Detail = $"Message kind {messageKind} not accepted by binding {binding.Name}",
				Instance = context.MessageId ?? Guid.NewGuid().ToString(),
			};
			return new Excalibur.Dispatch.Messaging.MessageResult(succeeded: false, problemDetails: problemDetails);
		}

		// Store adapter name for downstream use (binding is already in context)
		context.SetProperty("TransportAdapter", binding.TransportAdapter.Name);

		// Continue with the pipeline
		// NOTE: Pipeline profile selection is handled by the Dispatcher BEFORE middleware invocation, ensuring the correct pipeline is
		// constructed based on transport binding configuration.
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Uses reflection to check message interfaces")]
	private static MessageKinds DetermineMessageKind(IDispatchMessage message)
	{
		var type = message.GetType();
		var kinds = MessageKinds.None;

		if (typeof(IDispatchAction).IsAssignableFrom(type) ||
			type.GetInterfaces().Any(static i => i.IsGenericType &&
												 i.GetGenericTypeDefinition() == typeof(IDispatchAction<>)))
		{
			kinds |= MessageKinds.Action;
		}

		if (typeof(IDispatchEvent).IsAssignableFrom(type))
		{
			kinds |= MessageKinds.Event;
		}

		if (typeof(IDispatchDocument).IsAssignableFrom(type))
		{
			kinds |= MessageKinds.Document;
		}

		return kinds == MessageKinds.None ? MessageKinds.Document : kinds;
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.TransportMessageKindNotAccepted, LogLevel.Warning,
		"Message kind {MessageKind} not accepted by binding {BindingName}")]
	private partial void LogMessageKindNotAccepted(MessageKinds messageKind, string bindingName);
}
