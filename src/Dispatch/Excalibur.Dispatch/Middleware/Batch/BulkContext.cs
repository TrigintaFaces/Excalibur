// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// A context that represents a bulk collection of contexts.
/// </summary>
internal sealed class BulkContext : IMessageContext
{
	private static readonly IServiceProvider EmptyServiceProvider = new NullServiceProvider();

	public BulkContext(IList<IMessageContext> contexts)
	{
		Contexts = contexts;
		Items = new Dictionary<string, object>(StringComparer.Ordinal);
		Features = new Dictionary<Type, object>();

		// Use first context as primary context for bulk-level properties
		var primaryContext = contexts.Count > 0 ? contexts[0] : null;
		if (primaryContext != null)
		{
			MessageId = primaryContext.MessageId;
			CorrelationId = primaryContext.CorrelationId;
			CausationId = primaryContext.CausationId;
			RequestServices = primaryContext.RequestServices;

			// Copy features from primary context
			foreach (var kvp in primaryContext.Features)
			{
				Features[kvp.Key] = kvp.Value;
			}

			// Copy Items metadata
			SerializerVersion = primaryContext.SerializerVersion();
			MessageVersion = primaryContext.MessageVersion();
			ContractVersion = primaryContext.ContractVersion();
			DesiredVersion = int.TryParse(primaryContext.DesiredVersion(), out var version) ? version : null;
			PartitionKey = primaryContext.PartitionKey();
			ReplyTo = primaryContext.ReplyTo();
			MessageType = primaryContext.GetMessageType();
			ContentType = primaryContext.GetContentType();
			VersionMetadata = primaryContext.VersionMetadata() as IMessageVersionMetadata;
			ValidationResult = primaryContext.ValidationResult() as IValidationResult;
			AuthorizationResult = primaryContext.AuthorizationResult() as IAuthorizationResult;
			Metadata = primaryContext.Metadata() as IMessageMetadata;
		}
		else
		{
			MessageType = "BulkMessage";
			RequestServices = EmptyServiceProvider;
		}
	}

	public IList<IMessageContext> Contexts { get; }

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; }

	/// <inheritdoc />
	public IDictionary<string, object> Items { get; }

	/// <inheritdoc />
	public IDictionary<Type, object> Features { get; }

	// ========================================== CLASS-ONLY PROPERTIES ==========================================

	/// <summary>
	/// Gets or sets the serializer version.
	/// </summary>
	public string? SerializerVersion { get; set; }

	/// <summary>
	/// Gets or sets the message version.
	/// </summary>
	public string? MessageVersion { get; set; }

	/// <summary>
	/// Gets or sets the contract version.
	/// </summary>
	public string? ContractVersion { get; set; }

	/// <summary>
	/// Gets or sets the desired version.
	/// </summary>
	public int? DesiredVersion { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type.
	/// </summary>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the partition key.
	/// </summary>
	public string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the reply-to address.
	/// </summary>
	public string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the version metadata.
	/// </summary>
	public IMessageVersionMetadata? VersionMetadata { get; set; }

	/// <summary>
	/// Gets or sets the validation result.
	/// </summary>
	public IValidationResult? ValidationResult { get; set; }

	/// <summary>
	/// Gets or sets the authorization result.
	/// </summary>
	public IAuthorizationResult? AuthorizationResult { get; set; }

	/// <summary>
	/// Gets or sets the routing decision.
	/// </summary>
	public RoutingDecision? RoutingDecision { get; set; } =
		Abstractions.Routing.RoutingDecision.Local;

	/// <summary>
	/// Gets a value indicating whether all validation, authorization, and routing checks passed.
	/// </summary>
	public bool Success =>
		(ValidationResult?.IsValid ?? true) &&
		(AuthorizationResult?.IsAuthorized ?? true) &&
		(RoutingDecision?.IsSuccess ?? true);

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	public IMessageMetadata? Metadata { get; set; }

	/// <summary>
	/// Lightweight service provider that returns null for all service requests.
	/// </summary>
	private sealed class NullServiceProvider : IServiceProvider
	{
		public object? GetService(Type serviceType) => null;
	}
}
