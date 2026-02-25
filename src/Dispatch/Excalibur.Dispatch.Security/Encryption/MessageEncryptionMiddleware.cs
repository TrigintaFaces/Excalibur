// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that handles transparent message encryption and decryption.
/// </summary>
public sealed partial class MessageEncryptionMiddleware : IDispatchMiddleware
{
	private readonly IMessageEncryptionService _encryptionService;
	private readonly EncryptionOptions _options;
	private readonly ILogger<MessageEncryptionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEncryptionMiddleware" /> class.
	/// </summary>
	/// <param name="encryptionService"> The encryption service responsible for encrypting and decrypting messages. </param>
	/// <param name="options"> The encryption options. </param>
	/// <param name="logger"> The logger used for diagnostics. </param>
	public MessageEncryptionMiddleware(
		IMessageEncryptionService encryptionService,
		IOptions<EncryptionOptions> options,
		ILogger<MessageEncryptionMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(encryptionService);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_encryptionService = encryptionService;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Serialization;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:Using RequiresDynamicCode member in AOT",
			Justification = "Encryption middleware uses JSON serialization and reflection by design.")]
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
			Justification = "Encryption middleware relies on runtime serialization of message types.")]
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

		// Determine if this message should be encrypted
		var shouldEncrypt = ShouldEncryptMessage(message, context);
		if (!shouldEncrypt)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		using var activity = Activity.Current?.Source.StartActivity("Encryption.ProcessMessage");
		_ = (activity?.SetTag("encryption.enabled", value: true));

		try
		{
			// Check if message is incoming (needs decryption) or outgoing (needs encryption)
			var isIncoming = context.TryGetValue<string>("MessageDirection", out var direction) &&
							 string.Equals(direction, "Incoming", StringComparison.Ordinal);

			if (isIncoming)
			{
				// Decrypt incoming message
				await DecryptMessageAsync(message, context, cancellationToken).ConfigureAwait(false);
				_ = (activity?.SetTag("encryption.operation", "decrypt"));
			}
			else
			{
				// Process message first
				var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

				// Encrypt outgoing message after processing
				if (result.Succeeded)
				{
					await EncryptMessageAsync(message, context, cancellationToken).ConfigureAwait(false);
					_ = (activity?.SetTag("encryption.operation", "encrypt"));
				}

				return result;
			}

			// For incoming messages, continue processing after decryption
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (EncryptionException ex)
		{
			LogEncryptionFailed(ex, message.GetType().Name);
			_ = (activity?.SetTag("encryption.error", ex.Message));

			return MessageResult.Failed(MessageProblemDetails.InternalError("Message encryption/decryption failed"));
		}
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075",
		Justification = "Message types are known at runtime and used for JSON serialization")]
	private static PropertyInfo[] GetPublicProperties(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type type)
		=> type.GetProperties();

	private bool ShouldEncryptMessage(IDispatchMessage message, IMessageContext context)
	{
		// Check if message type is in the exclude list
		var messageType = message.GetType().Name;
		if (_options.ExcludedMessageTypes?.Contains(messageType) == true)
		{
			return false;
		}

		// Check if encryption is explicitly disabled in context
		if (context.TryGetValue<bool>("DisableEncryption", out var disable) &&
			disable)
		{
			return false;
		}

		// Check if message has sensitive data marker
		if (message is ISensitiveMessage)
		{
			return true;
		}

		// Use default behavior
		return _options.EncryptByDefault;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private async Task EncryptMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Build encryption context
		var encryptionContext = BuildEncryptionContext(context);

		// Serialize message to JSON
		var json = JsonSerializer.Serialize(message, message.GetType());

		// Encrypt the JSON
		var encrypted = await _encryptionService.EncryptMessageAsync(
			json,
			encryptionContext,
			cancellationToken).ConfigureAwait(false);

		// Store encrypted content in context for transport
		context.SetProperty("EncryptedPayload", encrypted);
		context.SetProperty("IsEncrypted", value: true);
		context.SetProperty("EncryptionAlgorithm", encryptionContext.Algorithm.ToString());

		LogMessageEncrypted(message.GetType().Name, encryptionContext.TenantId ?? "default");
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Message decryption requires runtime type resolution for arbitrary message types - reflection is intentional")]
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072",
		Justification = "Runtime type inspection is required for property copying during message decryption")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize(String, Type, JsonSerializerOptions)")]
	private async Task DecryptMessageAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Check if message is encrypted
		if (!context.TryGetValue<bool>("IsEncrypted", out var isEncrypted) || !isEncrypted)
		{
			return; // Message is not encrypted
		}

		// Get encrypted payload
		if (!context.TryGetValue<string>("EncryptedPayload", out var payload) ||
			payload is not { })
		{
			throw new DecryptionException(
					Resources.MessageEncryptionMiddleware_EncryptedPayloadNotFound);
		}

		// Build decryption context
		var encryptionContext = BuildEncryptionContext(context);

		// Decrypt the payload
		var decrypted = await _encryptionService.DecryptMessageAsync(
			payload,
			encryptionContext,
			cancellationToken).ConfigureAwait(false);

		// Deserialize back to message object
		var messageType = message.GetType();
		var decryptedMessage = JsonSerializer.Deserialize(decrypted, messageType);

		// Copy properties to original message atomically — read all values first, then apply
		if (decryptedMessage != null)
		{
			// IL2072: Call to GetPublicProperties is safe because message types are known at compile time
			var properties = GetPublicProperties(messageType);

			// Phase 1: Read all values into a buffer (safe — no mutation)
			var values = new (PropertyInfo Prop, object? Value)[properties.Length];
			var count = 0;
			foreach (var prop in properties)
			{
				if (prop is { CanWrite: true, CanRead: true })
				{
					values[count++] = (prop, prop.GetValue(decryptedMessage));
				}
			}

			// Phase 2: Apply all values (minimizes partial-update window)
			for (var i = 0; i < count; i++)
			{
				values[i].Prop.SetValue(message, values[i].Value);
			}
		}

		// Remove encryption markers from context
		context.RemoveItem("EncryptedPayload");
		context.SetProperty("WasEncrypted", value: true);

		LogMessageDecrypted(message.GetType().Name, encryptionContext.TenantId ?? "default");
	}

	private EncryptionContext BuildEncryptionContext(IMessageContext messageContext)
	{
		// Extract values from message context
		_ = messageContext.TryGetValue<string>("TenantId", out var tenantId);
		_ = messageContext.TryGetValue<string>("EncryptionKeyId", out var keyId);
		_ = messageContext.TryGetValue<string>("EncryptionPurpose", out var purpose);

		// Build immutable context with all values at once
		return new EncryptionContext
		{
			Algorithm = _options.DefaultAlgorithm,
			TenantId = tenantId,
			KeyId = keyId ?? _options.CurrentKeyId,
			Purpose = purpose,
		};
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.EncryptionMiddlewareEncryptionFailed, LogLevel.Error,
		"Encryption operation failed for message {MessageType}")]
	private partial void LogEncryptionFailed(Exception ex, string messageType);

	[LoggerMessage(SecurityEventId.EncryptionMiddlewareMessageEncrypted, LogLevel.Debug,
		"Encrypted message {MessageType} for tenant {TenantId}")]
	private partial void LogMessageEncrypted(string messageType, string tenantId);

	[LoggerMessage(SecurityEventId.EncryptionMiddlewareMessageDecrypted, LogLevel.Debug,
		"Decrypted message {MessageType} for tenant {TenantId}")]
	private partial void LogMessageDecrypted(string messageType, string tenantId);
}
