// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for establishing and validating tenant identity context in multi-tenant scenarios where message processing needs
/// to be isolated per tenant.
/// </summary>
/// <remarks>
/// This middleware operates early in the pipeline to ensure tenant context is available to all subsequent middleware and handlers. It:
/// <list type="bullet">
/// <item> Extracts tenant identifiers from message headers or context </item>
/// <item> Validates tenant access and permissions </item>
/// <item> Establishes tenant isolation boundaries </item>
/// <item> Propagates tenant context through the message flow </item>
/// <item> Integrates with structured logging and tracing </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.All)]
public sealed partial class TenantIdentityMiddleware : IDispatchMiddleware
{
	private readonly TenantIdentityOptions _options;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<TenantIdentityMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantIdentityMiddleware" /> class. Creates a new tenant identity middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for tenant identity processing. </param>
	/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public TenantIdentityMiddleware(
		IOptions<TenantIdentityOptions> options,
		ITelemetrySanitizer sanitizer,
		ILogger<TenantIdentityMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(sanitizer);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_sanitizer = sanitizer;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip processing if tenant identity is disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract and validate tenant context
		var tenantContext =
			await ResolveTenantContextAsync(message, context, cancellationToken)
				.ConfigureAwait(false);

		// Validate tenant access if validation is enabled
		if (_options.ValidateTenantAccess)
		{
			await ValidateTenantAccessAsync(tenantContext, context, cancellationToken).ConfigureAwait(false);
		}

		// Set tenant context for downstream middleware and handlers
		SetTenantContext(context, tenantContext);

		// Set up logging scope with tenant information
		using var logScope = CreateTenantLoggingScope(tenantContext);

		// Set up OpenTelemetry activity tags
		SetTenantActivityTags(tenantContext);

		LogTenantContextEstablished(message.GetType().Name, tenantContext.TenantId);

		try
		{
			// Continue pipeline execution with tenant context
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			LogTenantContextMaintained(tenantContext.TenantId);

			return result;
		}
		catch (Exception ex)
		{
			LogTenantProcessingException(tenantContext.TenantId, ex);
			throw;
		}
	}

	/// <summary>
	/// Extracts tenant ID from message properties or attributes.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	private static string? ExtractTenantFromMessage(IDispatchMessage message)
	{
		// Check if message has a TenantId property
		var messageType = message.GetType();
		var tenantProperty = messageType.GetProperty("TenantId");

		if (tenantProperty?.CanRead == true)
		{
			var tenantId = tenantProperty.GetValue(message);
			return tenantId?.ToString();
		}

		return null;
	}

	/// <summary>
	/// Extracts a header value from the message context.
	/// </summary>
	private static string? ExtractHeaderValue(IMessageContext context, string? headerName)
	{
		if (string.IsNullOrEmpty(headerName))
		{
			return null;
		}

		var value = context.GetItem<object>(headerName);
		return value?.ToString();
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for tenant tracing.
	/// </summary>
	private void SetTenantActivityTags(TenantContext tenantContext)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		SetSanitizedTag(activity, "tenant.id", tenantContext.TenantId);

		if (!string.IsNullOrEmpty(tenantContext.TenantName))
		{
			SetSanitizedTag(activity, "tenant.name", tenantContext.TenantName);
		}

		if (!string.IsNullOrEmpty(tenantContext.TenantRegion))
		{
			_ = activity.SetTag("tenant.region", tenantContext.TenantRegion);
		}
	}

	private void SetSanitizedTag(Activity activity, string tagName, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(tagName, rawValue);
		if (sanitized is not null)
		{
			_ = activity.SetTag(tagName, sanitized);
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.TenancyMiddlewareExecuting, LogLevel.Debug,
		"Established tenant context for message {MessageType} with tenant ID {TenantId}")]
	private partial void LogTenantContextEstablished(string messageType, string tenantId);

	[LoggerMessage(MiddlewareEventId.TenantContextSet, LogLevel.Debug,
		"Tenant context maintained through pipeline execution for tenant {TenantId}")]
	private partial void LogTenantContextMaintained(string tenantId);

	[LoggerMessage(MiddlewareEventId.TenantContextSet + 10, LogLevel.Error,
		"Exception occurred during message processing for tenant {TenantId}")]
	private partial void LogTenantProcessingException(string tenantId, Exception ex);

	[LoggerMessage(MiddlewareEventId.TenantIdentified, LogLevel.Debug,
		"Using default tenant ID: {TenantId}")]
	private partial void LogUsingDefaultTenant(string tenantId);

	[LoggerMessage(MiddlewareEventId.TenantNotFound, LogLevel.Error,
		"Tenant identification failed for message {MessageType}")]
	private partial void LogTenantIdentificationFailed(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.TenantIdentified + 10, LogLevel.Debug,
		"Resolved tenant ID: {TenantId}")]
	private partial void LogTenantResolved(string tenantId);

	[LoggerMessage(MiddlewareEventId.TenantNotFound + 10, LogLevel.Warning,
		"Tenant access validation failed for tenant {TenantId}")]
	private partial void LogTenantAccessValidationFailed(string tenantId, Exception ex);

	[LoggerMessage(MiddlewareEventId.TenantIdentified + 20, LogLevel.Debug,
		"Tenant access validation passed for tenant {TenantId}")]
	private partial void LogTenantAccessValidationPassed(string tenantId);

	/// <summary>
	/// Resolves tenant context from message headers, context, or default sources.
	/// </summary>
	/// <exception cref="InvalidOperationException"> </exception>
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future async tenant resolution support")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future async tenant resolution support")]
	private Task<TenantContext> ResolveTenantContextAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Try to extract tenant ID from configured header
		var tenantId = ExtractHeaderValue(context, _options.TenantIdHeader);

		// Fall back to extracting from message properties
		if (string.IsNullOrEmpty(tenantId))
		{
			tenantId = ExtractTenantFromMessage(message);
		}

		// Fall back to default tenant if configured
		if (string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(_options.DefaultTenantId))
		{
			tenantId = _options.DefaultTenantId;
			LogUsingDefaultTenant(tenantId);
		}

		// Validate that we have a tenant ID
		if (string.IsNullOrEmpty(tenantId))
		{
			var exception = new InvalidOperationException(
				Resources.TenantIdentityMiddleware_NoTenantIdentifierFound);
			LogTenantIdentificationFailed(message.GetType().Name, exception);
			throw exception;
		}

		LogTenantResolved(tenantId);

		// Create tenant context with additional metadata
		var tenantContext = new TenantContext(
			tenantId,
			ExtractHeaderValue(context, _options.TenantNameHeader),
			ExtractHeaderValue(context, _options.TenantRegionHeader));

		return Task.FromResult(tenantContext);
	}

	/// <summary>
	/// Validates tenant access permissions (extensibility hook for custom validation).
	/// </summary>
	/// <exception cref="UnauthorizedAccessException"> </exception>
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "Context and CancellationToken parameters reserved for future custom tenant validation logic and async operations")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Context and CancellationToken parameters reserved for future custom tenant validation logic and async operations")]
	private Task ValidateTenantAccessAsync(
		TenantContext tenantContext,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Basic validation - ensure tenant ID is valid format
		if (!IsValidTenantId(tenantContext.TenantId))
		{
			var exception = new UnauthorizedAccessException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.TenantIdentityMiddleware_InvalidTenantIdentifierFormat,
					tenantContext.TenantId));
			LogTenantAccessValidationFailed(tenantContext.TenantId, exception);
			throw exception;
		}

		// Future extensibility: This is where custom tenant validation logic would be plugged in For example, checking against a tenant
		// registry, validating subscription status, etc.
		LogTenantAccessValidationPassed(tenantContext.TenantId);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Validates the format of a tenant identifier.
	/// </summary>
	private bool IsValidTenantId(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			return false;
		}

		// Basic validation - adjust based on your tenant ID format requirements
		if (tenantId.Length < _options.MinTenantIdLength || tenantId.Length > _options.MaxTenantIdLength)
		{
			return false;
		}

		// Check for invalid characters if pattern is specified
		if (!string.IsNullOrEmpty(_options.TenantIdPattern) &&
			!Regex.IsMatch(tenantId, _options.TenantIdPattern))
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Sets tenant context in the message context for downstream access.
	/// </summary>
	private void SetTenantContext(IMessageContext context, TenantContext tenantContext)
	{
		// Set tenant properties in context for downstream middleware
		context.SetItem("TenantId", tenantContext.TenantId);

		if (!string.IsNullOrEmpty(tenantContext.TenantName))
		{
			context.SetItem("TenantName", tenantContext.TenantName);
		}

		if (!string.IsNullOrEmpty(tenantContext.TenantRegion))
		{
			context.SetItem("TenantRegion", tenantContext.TenantRegion);
		}

		// Set in headers for outbound propagation if header names are configured
		if (!string.IsNullOrEmpty(_options.TenantIdHeader))
		{
			context.SetItem(_options.TenantIdHeader, tenantContext.TenantId);
		}

		if (!string.IsNullOrEmpty(_options.TenantNameHeader) && !string.IsNullOrEmpty(tenantContext.TenantName))
		{
			context.SetItem(_options.TenantNameHeader, tenantContext.TenantName);
		}

		if (!string.IsNullOrEmpty(_options.TenantRegionHeader) && !string.IsNullOrEmpty(tenantContext.TenantRegion))
		{
			context.SetItem(_options.TenantRegionHeader, tenantContext.TenantRegion);
		}
	}

	/// <summary>
	/// Creates a logging scope with tenant information.
	/// </summary>
	private IDisposable? CreateTenantLoggingScope(TenantContext tenantContext)
	{
		var scopeProperties = new Dictionary<string, object>(StringComparer.Ordinal) { ["TenantId"] = tenantContext.TenantId };

		if (!string.IsNullOrEmpty(tenantContext.TenantName))
		{
			scopeProperties["TenantName"] = tenantContext.TenantName;
		}

		if (!string.IsNullOrEmpty(tenantContext.TenantRegion))
		{
			scopeProperties["TenantRegion"] = tenantContext.TenantRegion;
		}

		return _logger.BeginScope(scopeProperties);
	}

	/// <summary>
	/// Internal structure to hold tenant context during processing.
	/// </summary>
	private readonly record struct TenantContext(
		string TenantId,
		string? TenantName,
		string? TenantRegion);
}
