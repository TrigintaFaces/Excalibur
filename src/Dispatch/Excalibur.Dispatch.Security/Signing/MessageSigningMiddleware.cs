// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that handles message signing and signature verification.
/// </summary>
public sealed partial class MessageSigningMiddleware : IDispatchMiddleware
{
	private readonly IMessageSigningService _signingService;
	private readonly SigningOptions _options;
	private readonly ILogger<MessageSigningMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageSigningMiddleware" /> class.
	/// </summary>
	/// <param name="signingService">The signing service used to create and verify signatures.</param>
	/// <param name="options">The signing options.</param>
	/// <param name="logger">The logger used for diagnostics.</param>
	public MessageSigningMiddleware(
		IMessageSigningService signingService,
		IOptions<SigningOptions> options,
		ILogger<MessageSigningMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(signingService);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_signingService = signingService;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:Using RequiresDynamicCode member in AOT",
			Justification = "Message signing inspects message metadata at runtime.")]
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
			Justification = "Message signing uses JSON serialization for message payloads.")]
	public async ValueTask<IMessageResult> InvokeAsync(
	IDispatchMessage message,
	IMessageContext context,
	DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		using var activity = Activity.Current?.Source.StartActivity("Signing.ProcessMessage");

		// Check if message is incoming (needs verification) or outgoing (needs signing)
		var isIncoming = IsIncomingMessage(context);

		try
		{
			if (isIncoming)
			{
				return await ProcessIncomingMessageAsync(message, context, nextDelegate, activity, cancellationToken).ConfigureAwait(false);
			}

			return await ProcessOutgoingMessageAsync(message, context, nextDelegate, activity, cancellationToken).ConfigureAwait(false);
		}
		catch (SigningException ex)
		{
			LogSigningOperationFailed(ex, message.GetType().Name);
			_ = (activity?.SetTag("signature.error", ex.Message));

			return MessageResult.Failed(MessageProblemDetails.InternalError("Message signing/verification failed"));
		}
	}

	private static bool IsIncomingMessage(IMessageContext context) => context.TryGetValue<string>("MessageDirection", out var direction) &&
			   string.Equals(direction, "Incoming", StringComparison.Ordinal);

	[RequiresUnreferencedCode("Message signing uses JSON serialization which may require unreferenced types")]
	[RequiresDynamicCode("Message signing uses reflection to access message properties and types")]
	private async Task<IMessageResult> ProcessIncomingMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		Activity? activity,
		CancellationToken cancellationToken)
	{
		var isValid = await VerifyMessageSignatureAsync(message, context, cancellationToken).ConfigureAwait(false);

		if (!isValid && _options.RequireValidSignature)
		{
			LogInvalidSignature(
				message.GetType().Name,
				context.TryGetValue<string>("TenantId", out var tid) ? tid ?? "unknown" : "unknown");

			_ = (activity?.SetTag("signature.valid", value: false));

			return MessageResult.Failed(MessageProblemDetails.AuthorizationError("Message signature verification failed"));
		}

		_ = (activity?.SetTag("signature.valid", isValid));

		// For incoming messages, continue processing after verification
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Message signing uses JSON serialization which may require unreferenced types")]
	[RequiresDynamicCode("Message signing uses reflection to access message properties and types")]
	private async Task<IMessageResult> ProcessOutgoingMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		Activity? activity,
		CancellationToken cancellationToken)
	{
		// Process message first
		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Sign outgoing message after processing
		if (result.Succeeded)
		{
			await SignMessageAsync(message, context, cancellationToken).ConfigureAwait(false);
			_ = (activity?.SetTag("signature.created", value: true));
		}

		return result;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private async Task SignMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Build signing context
		var signingContext = BuildSigningContext(context);

		// Serialize message for signing
		var json = JsonSerializer.Serialize(message, message.GetType());

		// Create signature
		var signature = await _signingService.SignMessageAsync(
			json,
			signingContext,
			cancellationToken).ConfigureAwait(false);

		// Store signature in context
		context.SetProperty("MessageSignature", signature);
		context.SetProperty("SignatureAlgorithm", signingContext.Algorithm.ToString());
		context.SetProperty("SignedAt", DateTimeOffset.UtcNow);

		LogMessageSigned(message.GetType().Name, signingContext.Algorithm);
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private async Task<bool> VerifyMessageSignatureAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Check if message has a signature
		if (!context.TryGetValue<string>("MessageSignature", out var signature) ||
			signature is not { })
		{
			// No signature present
			if (_options.RequireValidSignature)
			{
				LogNoSignatureFound(message.GetType().Name);
				return false;
			}

			return true; // Allow unsigned messages if not required
		}

		// Build signing context
		var signingContext = BuildSigningContext(context);

		// Get algorithm from context if available
		if (context.TryGetValue<string>("SignatureAlgorithm", out var algorithm) &&
			Enum.TryParse<SigningAlgorithm>(algorithm, out var alg))
		{
			signingContext.Algorithm = alg;
		}

		// Serialize message for verification
		var json = JsonSerializer.Serialize(message, message.GetType());

		// Verify signature
		var isValid = await _signingService.VerifySignatureAsync(
			json,
			signature,
			signingContext,
			cancellationToken).ConfigureAwait(false);

		if (isValid)
		{
			context.SetProperty("SignatureVerified", value: true);
			LogSignatureVerified(message.GetType().Name);
		}
		else
		{
			LogSignatureVerificationFailed(message.GetType().Name);
		}

		return isValid;
	}

	private SigningContext BuildSigningContext(IMessageContext messageContext)
	{
		var context = new SigningContext
		{
			Algorithm = _options.DefaultAlgorithm,
			IncludeTimestamp = _options.IncludeTimestampByDefault,
			Format = SignatureFormat.Base64,
		};

		// Extract tenant ID and apply per-tenant algorithm override if configured
		if (messageContext.TryGetValue<string>("TenantId", out var tenantId) &&
			tenantId != null)
		{
			context.TenantId = tenantId;

			if (_options.TenantAlgorithms.TryGetValue(tenantId, out var tenantAlgorithm))
			{
				context.Algorithm = tenantAlgorithm;
			}
		}

		// Extract key ID if specified
		if (messageContext.TryGetValue<string>("SigningKeyId", out var keyId) &&
			keyId != null)
		{
			context.KeyId = keyId;
		}
		else
		{
			context.KeyId = _options.DefaultKeyId;
		}

		// Extract purpose if specified
		if (messageContext.TryGetValue<string>("SigningPurpose", out var purpose) &&
			purpose != null)
		{
			context.Purpose = purpose;
		}

		return context;
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.SigningMiddlewareInvalidSignature, LogLevel.Warning, "Invalid signature for message {MessageType} from tenant {TenantId}")]
	private partial void LogInvalidSignature(string messageType, string tenantId);

	[LoggerMessage(SecurityEventId.SigningMiddlewareOperationFailed, LogLevel.Error, "Signing operation failed for message {MessageType}")]
	private partial void LogSigningOperationFailed(Exception ex, string messageType);

	[LoggerMessage(SecurityEventId.SigningMiddlewareMessageSigned, LogLevel.Debug, "Signed message {MessageType} with algorithm {Algorithm}")]
	private partial void LogMessageSigned(string messageType, SigningAlgorithm algorithm);

	[LoggerMessage(SecurityEventId.SigningMiddlewareNoSignatureFound, LogLevel.Warning, "No signature found for message {MessageType}")]
	private partial void LogNoSignatureFound(string messageType);

	[LoggerMessage(SecurityEventId.SigningMiddlewareSignatureVerified, LogLevel.Debug, "Successfully verified signature for message {MessageType}")]
	private partial void LogSignatureVerified(string messageType);

	[LoggerMessage(SecurityEventId.SigningMiddlewareSignatureVerificationFailed, LogLevel.Warning, "Signature verification failed for message {MessageType}")]
	private partial void LogSignatureVerificationFailed(string messageType);
}
