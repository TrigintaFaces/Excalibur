// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Synthesizes default pipeline profiles when none are explicitly registered. Implements requirements R7.5-R7.12 for automatic pipeline synthesis.
/// </summary>
/// <remarks> Creates a new pipeline profile synthesizer. </remarks>
/// <param name="logger"> Logger for synthesis diagnostics. </param>
/// <param name="options"> Dispatch configuration options. </param>
/// <param name="applicabilityStrategy"> Strategy for determining middleware applicability. </param>
// R0.8: Parameter 'applicabilityStrategy' is unread - reserved for future middleware filtering
#pragma warning disable CS9113

public sealed partial class PipelineProfileSynthesizer(
	ILogger<PipelineProfileSynthesizer> logger,
	IOptions<DispatchOptions> options,
	IMiddlewareApplicabilityStrategy applicabilityStrategy)
{
	/// <summary>
	/// Represents the default baseline middleware order as per R7.6.
	/// </summary>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level before middleware runs.
	/// </remarks>
	private static readonly MiddlewareDefinition[] DefaultMiddlewareOrder =
	[
		// Note: CorrelationMiddleware removed in Sprint 70 - correlation now handled at Dispatcher level
		new(typeof(TenantIdentityMiddleware), MessageKinds.All, "TenantIdentity", DispatchFeature.MultiTenancy),
		new(typeof(ContractVersionCheckMiddleware), MessageKinds.Event | MessageKinds.Document, "ContractVersionCheck",
			DispatchFeature.Versioning),
		new(typeof(ValidationMiddleware), MessageKinds.Action, "Validation", DispatchFeature.Validation),
		new(typeof(AuthorizationMiddleware), MessageKinds.Action, "Authorization", DispatchFeature.Authorization),
		new(typeof(TimeoutMiddleware), MessageKinds.Action | MessageKinds.Event, "Timeout", DispatchFeature.Timeout),
		new(typeof(TransactionMiddleware), MessageKinds.Action, "Transaction", DispatchFeature.Transaction),
		new(typeof(OutboxStagingMiddleware), MessageKinds.Action | MessageKinds.Event, "OutboxStaging", DispatchFeature.Outbox),
		new(typeof(MetricsLoggingMiddleware), MessageKinds.All, "MetricsLogging", DispatchFeature.Metrics),
	];

	private readonly ILogger<PipelineProfileSynthesizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly DispatchOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	private readonly List<ValidationIssue> _validationIssues = [];

	/// <summary>
	/// Represents available dispatch features for feature-aware synthesis.
	/// </summary>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level before middleware runs,
	/// so it's no longer a feature toggle for middleware selection.
	/// </remarks>
	private enum DispatchFeature
	{
		MultiTenancy = 0,
		Versioning = 1,
		Validation = 2,
		Authorization = 3,
		Timeout = 4,
		Transaction = 5,
		Outbox = 6,
		Metrics = 7,
	}

	/// <summary>
	/// Synthesizes a default pipeline profile based on enabled features and message kinds.
	/// </summary>
	/// <param name="profileName"> Name of the profile to synthesize (default: "default"). </param>
	/// <returns> The synthesized pipeline profile. </returns>
	public IPipelineProfile SynthesizeDefaultProfile(string profileName = "default")
	{
		LogSynthesizingDefaultProfile(profileName);
		_validationIssues.Clear();

		var middlewareTypes = new List<Type>();
		var omittedMiddleware = new List<string>();

		// Build middleware list based on feature availability (R7.10)
		foreach (var definition in DefaultMiddlewareOrder)
		{
			if (IsFeatureEnabled(definition.RequiredFeature))
			{
				middlewareTypes.Add(definition.MiddlewareType);
				LogIncludingMiddleware(definition.Name, definition.ApplicableKinds);
			}
			else
			{
				omittedMiddleware.Add(definition.Name);
				LogOmittingMiddleware(definition.Name, definition.RequiredFeature);

				_validationIssues.Add(new ValidationIssue(
					ValidationSeverity.Warning,
					$"Middleware '{definition.Name}' omitted - feature '{definition.RequiredFeature}' is disabled"));
			}
		}

		// Log synthesis summary (R7.11)
		LogPipelineSynthesisComplete(middlewareTypes.Count, omittedMiddleware.Count);

		if (omittedMiddleware.Count > 0)
		{
			LogOmittedMiddlewareWarning(string.Join(", ", omittedMiddleware));
		}

		// Create the synthesized profile
		var profile = new SynthesizedPipelineProfile(
			profileName,
			"Automatically synthesized default pipeline profile",
			[.. middlewareTypes],
			isStrict: false,
			supportedMessageKinds: MessageKinds.All,
			includedCount: middlewareTypes.Count,
			omittedCount: omittedMiddleware.Count);

		LogSynthesisSuccess(profileName, middlewareTypes.Count);

		return profile;
	}

	/// <summary>
	/// Synthesizes all required default profiles for the messaging system.
	/// </summary>
	/// <returns> Collection of synthesized profiles with their mappings. </returns>
	/// <exception cref="PipelineSynthesisException"></exception>
	public SynthesisResult SynthesizeRequiredProfiles()
	{
		LogBeginningSynthesis();
		_validationIssues.Clear();

		var profiles = new Dictionary<string, IPipelineProfile>(StringComparer.Ordinal);
		var mappings = new Dictionary<MessageKinds, string>();

		// Synthesize the default profile (R7.5)
		var defaultProfile = SynthesizeDefaultProfile();
		profiles["default"] = defaultProfile;

		// Create default mappings (R7.7)
		mappings[MessageKinds.Event] = "default";
		mappings[MessageKinds.Action] = "default";
		mappings[MessageKinds.Document] = "default";

		LogMappedMessageKinds();

		// Check for ambiguous mappings (R7.11)
		ValidateMappings(mappings);

		var result = new SynthesisResult(
			profiles,
			mappings,
			[.. _validationIssues]);

		if (result.HasErrors)
		{
			LogSynthesisError(result.Errors.Count());

			// Fail the build if there are errors
			throw new PipelineSynthesisException(
					ErrorMessages.PipelineSynthesisFailedWithErrors,
					result.ValidationIssues);
		}

		LogSynthesisResult(profiles.Count, mappings.Count);

		return result;
	}

	/// <summary>
	/// Determines if a specific dispatch feature is enabled.
	/// </summary>
	/// <remarks>
	/// Correlation is handled at the Dispatcher level and is always enabled,
	/// so it's no longer a feature toggle for middleware selection.
	/// </remarks>
	private bool IsFeatureEnabled(DispatchFeature feature) =>
		feature switch
		{
			DispatchFeature.MultiTenancy => _options.Features.EnableMultiTenancy,
			DispatchFeature.Versioning => _options.Features.EnableVersioning,
			DispatchFeature.Validation => _options.Features.ValidateMessageSchemas,
			DispatchFeature.Authorization => _options.Features.EnableAuthorization,
			DispatchFeature.Timeout => _options.DefaultTimeout > TimeSpan.Zero,
			DispatchFeature.Transaction => _options.Features.EnableTransactions,
			DispatchFeature.Outbox => _options.Outbox.Enabled,
			DispatchFeature.Metrics => _options.Features.EnableMetrics,
			_ => true, // Unknown features default to enabled
		};

	/// <summary>
	/// Validates pipeline mappings for ambiguity and completeness.
	/// </summary>
	private void ValidateMappings(Dictionary<MessageKinds, string> mappings)
	{
		// Check for missing mappings
		foreach (var kind in Enum.GetValues<MessageKinds>())
		{
			if (kind is MessageKinds.None or MessageKinds.All)
			{
				continue;
			}

			if (!mappings.ContainsKey(kind))
			{
				_validationIssues.Add(new ValidationIssue(
					ValidationSeverity.Warning,
					$"No pipeline mapping found for message kind '{kind}'"));
			}
		}

		// Check for duplicate or conflicting mappings
		var reverseMap = new Dictionary<string, List<MessageKinds>>(StringComparer.Ordinal);
		foreach (var (kind, profileName) in mappings)
		{
			if (!reverseMap.TryGetValue(profileName, out var list))
			{
				list = [];
				reverseMap[profileName] = list;
			}

			list.Add(kind);
		}

		// Log mapping summary
		foreach (var (profileName, kinds) in reverseMap)
		{
			LogProfileHandlesKinds(profileName, string.Join(", ", kinds));
		}
	}

	/// <summary>
	/// Represents a middleware definition for synthesis.
	/// </summary>
	private sealed record MiddlewareDefinition(
		Type MiddlewareType,
		MessageKinds ApplicableKinds,
		string Name,
		DispatchFeature RequiredFeature);

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.SynthesizingDefaultProfile, LogLevel.Information,
		"Synthesizing default pipeline profile '{ProfileName}'")]
	private partial void LogSynthesizingDefaultProfile(string profileName);

	[LoggerMessage(CoreEventId.MiddlewareIncluded, LogLevel.Debug,
		"Including middleware '{MiddlewareName}' for message kinds: {MessageKinds}")]
	private partial void LogIncludingMiddleware(string middlewareName, MessageKinds messageKinds);

	[LoggerMessage(CoreEventId.MiddlewareOmitted, LogLevel.Warning,
		"Omitting middleware '{MiddlewareName}' - required feature '{Feature}' is disabled")]
	private partial void LogOmittingMiddleware(string middlewareName, DispatchFeature feature);

	[LoggerMessage(CoreEventId.SynthesisComplete, LogLevel.Information,
		"Pipeline synthesis complete. Included: {IncludedCount} middleware, Omitted: {OmittedCount} middleware")]
	private partial void LogPipelineSynthesisComplete(int includedCount, int omittedCount);

	[LoggerMessage(CoreEventId.OmittedMiddlewareWarning, LogLevel.Warning,
		"The following middleware were omitted from the synthesized pipeline: {OmittedMiddleware}")]
	private partial void LogOmittedMiddlewareWarning(string omittedMiddleware);

	[LoggerMessage(CoreEventId.SynthesisSuccess, LogLevel.Information,
		"Successfully synthesized pipeline profile '{ProfileName}' with {MiddlewareCount} middleware components")]
	private partial void LogSynthesisSuccess(string profileName, int middlewareCount);

	[LoggerMessage(CoreEventId.SynthesisBeginning, LogLevel.Information,
		"Beginning synthesis of required pipeline profiles")]
	private partial void LogBeginningSynthesis();

	[LoggerMessage(CoreEventId.MappedMessageKinds, LogLevel.Information,
		"Mapped all message kinds to synthesized 'default' profile: Event→default, Action→default, Document→default")]
	private partial void LogMappedMessageKinds();

	[LoggerMessage(CoreEventId.SynthesisError, LogLevel.Error,
		"Pipeline synthesis completed with errors. Error count: {ErrorCount}")]
	private partial void LogSynthesisError(int errorCount);

	[LoggerMessage(CoreEventId.SynthesisResult, LogLevel.Information,
		"Successfully synthesized {ProfileCount} pipeline profiles with {MappingCount} mappings")]
	private partial void LogSynthesisResult(int profileCount, int mappingCount);

	[LoggerMessage(CoreEventId.ProfileHandlesKinds, LogLevel.Debug,
		"Profile '{ProfileName}' handles message kinds: {MessageKinds}")]
	private partial void LogProfileHandlesKinds(string profileName, string messageKinds);

	#endregion
}
