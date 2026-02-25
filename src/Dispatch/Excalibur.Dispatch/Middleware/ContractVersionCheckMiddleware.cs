// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for validating event contract versions and compatibility to ensure safe evolution of event schemas in distributed systems.
/// </summary>
/// <remarks>
/// This middleware applies specifically to Event messages to enforce contract versioning and backward/forward compatibility rules. It:
/// <list type="bullet">
/// <item> Validates event schema versions against supported versions </item>
/// <item> Enforces semantic versioning compatibility rules </item>
/// <item> Handles version migration and transformation </item>
/// <item> Provides warnings for deprecated event versions </item>
/// <item> Blocks processing of incompatible event versions </item>
/// <item> Integrates with schema registries for centralized version management </item>
/// </list>
/// Events are critical for system integration and require careful version management to prevent breaking changes from causing cascading
/// failures across services.
/// </remarks>
public sealed partial class ContractVersionCheckMiddleware : IDispatchMiddleware
{
	private readonly ContractVersionCheckOptions _options;

	private readonly IContractVersionService _versionService;

	private readonly ILogger<ContractVersionCheckMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContractVersionCheckMiddleware" /> class. Creates a new contract version check
	/// middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for contract version checking. </param>
	/// <param name="versionService"> Service for managing contract versions and compatibility. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public ContractVersionCheckMiddleware(
		IOptions<ContractVersionCheckOptions> options,
		IContractVersionService versionService,
		ILogger<ContractVersionCheckMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(versionService);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_versionService = versionService;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	/// <remarks>
	/// Contract version checking applies specifically to Event messages, as they represent published contracts between services that
	/// require version management. Actions (commands/queries) are typically handled within service boundaries and have different versioning requirements.
	/// </remarks>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Event;

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

		// Skip version checking if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract contract version information
		var versionContext = ExtractVersionContext(message, context);

		// Set up logging scope
		using var logScope = CreateVersionLoggingScope(message, versionContext);

		// Set up OpenTelemetry activity tags
		SetVersionActivityTags(message, versionContext);

		LogCheckingContractVersion(_logger, message.GetType().Name, versionContext.MessageVersion ?? "unspecified");

		try
		{
			// Validate contract version compatibility
			var compatibilityResult = await ValidateVersionCompatibilityAsync(
				message,
				versionContext,
				cancellationToken).ConfigureAwait(false);

			// Handle compatibility result
			await HandleCompatibilityResultAsync(
				message,
				versionContext,
				compatibilityResult,
				cancellationToken).ConfigureAwait(false);

			// Continue pipeline execution if compatible
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			LogContractVersionCheckPassed(_logger, message.GetType().Name, versionContext.MessageVersion ?? "unspecified");

			return result;
		}
		catch (Exception ex)
		{
			LogExceptionDuringContractVersionCheck(_logger, message.GetType().Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetPropertyValue(IMessageContext context, string? propertyName)
	{
		if (string.IsNullOrEmpty(propertyName))
		{
			return null;
		}

		// Use GetItem instead of Properties
		var value = context.GetItem<object>(propertyName);
		return value?.ToString();
	}

	/// <summary>
	/// Gets version information from message type attributes.
	/// </summary>
	private static string? GetVersionFromAttribute(Type messageType)
	{
		var versionAttr = messageType.GetCustomAttribute<ContractVersionAttribute>();
		return versionAttr?.Version;
	}

	/// <summary>
	/// Gets schema ID from message type attributes.
	/// </summary>
	private static string? GetSchemaIdFromAttribute(Type messageType)
	{
		var schemaAttr = messageType.GetCustomAttribute<SchemaIdAttribute>();
		return schemaAttr?.SchemaId;
	}

	/// <summary>
	/// Gets version from message properties.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	private static string? GetVersionFromProperty(IDispatchMessage message)
	{
		var messageType = message.GetType();
		var versionProperty = messageType.GetProperty("Version") ?? messageType.GetProperty("ContractVersion");

		if (versionProperty?.CanRead == true)
		{
			var version = versionProperty.GetValue(message);
			return version?.ToString();
		}

		return null;
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for version tracing.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future message-specific tracing enhancements")]
	private static void SetVersionActivityTags(
		IDispatchMessage message,
		ContractVersionContext versionContext)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("contract.message_type", versionContext.MessageType);
		_ = activity.SetTag("contract.version", versionContext.MessageVersion ?? "unspecified");
		_ = activity.SetTag("contract.schema_id", versionContext.SchemaId ?? "unknown");

		if (!string.IsNullOrEmpty(versionContext.ProducerService))
		{
			_ = activity.SetTag("contract.producer_service", versionContext.ProducerService);
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.ContractVersionMiddlewareExecuting, LogLevel.Debug,
		"Checking contract version for event {EventType} version {Version}")]
	private static partial void LogCheckingContractVersion(
				ILogger logger,
				string eventType,
				string version);

	[LoggerMessage(MiddlewareEventId.ContractVersionValidated, LogLevel.Debug,
		"Contract version check passed for event {EventType} version {Version}")]
	private static partial void LogContractVersionCheckPassed(
				ILogger logger,
				string eventType,
				string version);

	[LoggerMessage(MiddlewareEventId.ContractVersionMismatch, LogLevel.Error,
		"Exception occurred during contract version check for event {EventType}")]
	private static partial void LogExceptionDuringContractVersionCheck(
				ILogger logger,
				string eventType,
				Exception ex);

	[LoggerMessage(MiddlewareEventId.ContractVersionValidated + 10, LogLevel.Warning,
		"No version specified for event {EventType}, assuming compatibility")]
	private static partial void LogNoVersionSpecifiedAssumingCompatibility(
				ILogger logger,
				string eventType);

	[LoggerMessage(MiddlewareEventId.ContractUpgradeRequired, LogLevel.Warning,
		"Event {EventType} version {Version} is deprecated: {Reason}")]
	private static partial void LogEventVersionDeprecated(
				ILogger logger,
				string eventType,
				string version,
				string reason);

	[LoggerMessage(MiddlewareEventId.ContractVersionMismatch + 10, LogLevel.Error,
		"Contract version incompatibility for event {EventType} version {Version}: {Reason}")]
	private static partial void LogContractVersionIncompatibility(
				ILogger logger,
				string eventType,
				string version,
				string reason,
				Exception ex);

	[LoggerMessage(MiddlewareEventId.ContractVersionMismatch + 11, LogLevel.Warning,
		"Unknown version compatibility status for event {EventType} version {Version}: {Reason}")]
	private static partial void LogUnknownVersionCompatibilityStatus(
				ILogger logger,
				string eventType,
				string version,
				string reason);

	[LoggerMessage(MiddlewareEventId.ContractUpgradeRequired + 20, LogLevel.Information,
		"Recording deprecation metric for event {EventType} version {Version}")]
	private static partial void LogRecordingDeprecationMetric(
				ILogger logger,
				string eventType,
				string version);

	[LoggerMessage(MiddlewareEventId.ContractVersionMismatch + 12, LogLevel.Warning,
		"Failed to record deprecation metric for event {EventType}")]
	private static partial void LogFailedToRecordDeprecationMetric(
				ILogger logger,
				string eventType,
				Exception ex);

	/// <summary>
	/// Extracts version context from the message and context.
	/// </summary>
	private ContractVersionContext ExtractVersionContext(
		IDispatchMessage message,
		IMessageContext context)
	{
		var messageType = message.GetType();

		// Extract version from message headers
		var headerVersion = GetPropertyValue(context, _options.Headers.VersionHeaderName);

		// Extract version from message attributes
		var attributeVersion = GetVersionFromAttribute(messageType);

		// Extract version from message properties
		var propertyVersion = GetVersionFromProperty(message);

		// Use the first available version source based on priority
		var messageVersion = headerVersion ?? attributeVersion ?? propertyVersion;

		// Extract schema identifier
		var schemaId = GetPropertyValue(context, _options.Headers.SchemaIdHeaderName) ??
					   GetSchemaIdFromAttribute(messageType) ??
					   messageType.FullName;

		// Extract producer information
		var producerVersion = GetPropertyValue(context, _options.Headers.ProducerVersionHeaderName);
		var producerService = GetPropertyValue(context, _options.Headers.ProducerServiceHeaderName);

		return new ContractVersionContext(
			messageType.Name,
			messageVersion,
			schemaId,
			producerVersion,
			producerService,
			GetPropertyValue(context, "CorrelationId"));
	}

	/// <summary>
	/// Validates version compatibility using the version service.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future message-specific validation logic")]
	private async Task<VersionCompatibilityResult> ValidateVersionCompatibilityAsync(
		IDispatchMessage message,
		ContractVersionContext versionContext,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(versionContext.MessageVersion))
		{
			if (_options.RequireExplicitVersions)
			{
				return VersionCompatibilityResult.Incompatible(
					ErrorConstants.EventVersionNotSpecifiedAndExplicitVersionsRequired);
			}

			LogNoVersionSpecifiedAssumingCompatibility(_logger, versionContext.MessageType);

			return VersionCompatibilityResult.Compatible();
		}

		// Delegate to version service for compatibility check
		return await _versionService.CheckCompatibilityAsync(
			versionContext.SchemaId ?? string.Empty,
			versionContext.MessageVersion,
			_options.SupportedVersions,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles the result of version compatibility checking.
	/// </summary>
	/// <exception cref="ContractVersionException"> </exception>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future message-specific handling logic")]
	private async Task HandleCompatibilityResultAsync(
		IDispatchMessage message,
		ContractVersionContext versionContext,
		VersionCompatibilityResult compatibilityResult,
		CancellationToken cancellationToken)
	{
		switch (compatibilityResult.Status)
		{
			case VersionCompatibilityStatus.Compatible:
				// No action needed for compatible versions
				break;

			case VersionCompatibilityStatus.Deprecated:
				LogEventVersionDeprecated(_logger, versionContext.MessageType, versionContext.MessageVersion ?? "Unknown",
						compatibilityResult.Reason ?? "No reason provided");

				// Record deprecation metric if enabled
				if (_options.RecordDeprecationMetrics)
				{
					await RecordDeprecationMetricAsync(versionContext, cancellationToken).ConfigureAwait(false);
				}

				break;

			case VersionCompatibilityStatus.Incompatible:
				var exception = new ContractVersionException(
					string.Format(CultureInfo.InvariantCulture,
						Resources.ContractVersionCheckMiddleware_EventVersionIncompatible,
						versionContext.MessageType,
						versionContext.MessageVersion,
						compatibilityResult.Reason));

				LogContractVersionIncompatibility(_logger, versionContext.MessageType, versionContext.MessageVersion ?? "Unknown",
						compatibilityResult.Reason ?? "No reason provided", exception);

				if (_options.FailOnIncompatibleVersions)
				{
					throw exception;
				}

				break;

			case VersionCompatibilityStatus.Unknown:
				LogUnknownVersionCompatibilityStatus(_logger, versionContext.MessageType, versionContext.MessageVersion ?? "Unknown",
						compatibilityResult.Reason ?? "No reason provided");

				if (_options.FailOnUnknownVersions)
				{
					var unknownException = new ContractVersionException(
						string.Format(CultureInfo.InvariantCulture,
							Resources.ContractVersionCheckMiddleware_UnknownVersionStatusForEvent,
							versionContext.MessageType,
							versionContext.MessageVersion));

					throw unknownException;
				}

				break;

			default:
				goto case VersionCompatibilityStatus.Unknown;
		}
	}

	/// <summary>
	/// Records a deprecation metric for monitoring.
	/// </summary>
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency")]
	private async Task RecordDeprecationMetricAsync(
		ContractVersionContext versionContext,
		CancellationToken cancellationToken)
	{
		try
		{
			// This would integrate with your metrics system For now, just log it
			LogRecordingDeprecationMetric(_logger, versionContext.MessageType, versionContext.MessageVersion ?? "Unknown");

			await Task.CompletedTask.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToRecordDeprecationMetric(_logger, versionContext.MessageType, ex);
		}
	}

	/// <summary>
	/// Creates a logging scope with version context.
	/// </summary>
	/// <exception cref="InvalidOperationException"> </exception>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future message-specific logging enhancements")]
	private IDisposable CreateVersionLoggingScope(
		IDispatchMessage message,
		ContractVersionContext versionContext)
	{
		var scopeProperties = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["MessageType"] = versionContext.MessageType,
			["MessageVersion"] = versionContext.MessageVersion ?? "unspecified",
			["SchemaId"] = versionContext.SchemaId ?? "unknown",
		};

		if (!string.IsNullOrEmpty(versionContext.ProducerService))
		{
			scopeProperties["ProducerService"] = versionContext.ProducerService;
		}

		return _logger?.BeginScope(scopeProperties) ?? throw new InvalidOperationException(
			Resources.ContractVersionCheckMiddleware_LoggerNotInitialized);
	}

	/// <summary>
	/// Context information for contract version checking.
	/// </summary>
	private readonly record struct ContractVersionContext(
		string MessageType,
		string? MessageVersion,
		string? SchemaId,
		string? ProducerVersion,
		string? ProducerService,
		string? CorrelationId);
}
