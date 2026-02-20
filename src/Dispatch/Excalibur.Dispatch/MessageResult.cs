// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Default implementation of message result.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageResult" /> class. </remarks>
/// <param name="succeeded"> Whether the dispatch succeeded. </param>
/// <param name="problemDetails"> Problem details if failed. </param>
/// <param name="routingDecision"> Routing decision. </param>
/// <param name="validationResult"> Validation result. </param>
/// <param name="authorizationResult"> Authorization result. </param>
/// <param name="cacheHit"> Whether this was a cache hit. </param>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public class MessageResult(
		bool succeeded,
		IMessageProblemDetails? problemDetails = null,
		RoutingDecision? routingDecision = null,
		IValidationResult? validationResult = null,
		IAuthorizationResult? authorizationResult = null,
		bool cacheHit = false) : IMessageResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the dispatch succeeded.
	/// </summary>
	/// <value>The current <see cref="Succeeded"/> value.</value>
	public bool Succeeded { get; set; } = succeeded;

	/// <summary>
	/// Gets or sets problem details if the dispatch failed.
	/// </summary>
	/// <value>The current <see cref="ProblemDetails"/> value.</value>
	public IMessageProblemDetails? ProblemDetails { get; set; } = problemDetails;

	/// <summary>
	/// Gets or sets the routing decision.
	/// </summary>
	/// <value>The current <see cref="RoutingDecision"/> value.</value>
	public RoutingDecision? RoutingDecision { get; set; } = routingDecision;

	/// <summary>
	/// Gets or sets the validation result.
	/// </summary>
	/// <value>
	/// The validation result.
	/// </value>
	[JsonIgnore]
	public IValidationResult? ValidationResult { get; set; } = validationResult;

	/// <summary>
	/// Gets or sets the serializable validation result for JSON serialization. This property is used internally by the JSON serialization system.
	/// </summary>
	/// <value>
	/// The serializable validation result for JSON serialization. This property is used internally by the JSON serialization system.
	/// </value>
	[JsonPropertyName("validationResult")]
	public SerializableValidationResult? SerializableValidationResult
	{
		get => ValidationResult switch
		{
			null => null,
			SerializableValidationResult serializable => serializable,
			_ => new SerializableValidationResult { IsValid = ValidationResult.IsValid, Errors = ValidationResult.Errors?.ToList() ?? [] },
		};
		set => ValidationResult = value;
	}

	/// <summary>
	/// Gets or sets the authorization result.
	/// </summary>
	/// <value>The current <see cref="AuthorizationResult"/> value.</value>
	public IAuthorizationResult? AuthorizationResult { get; set; } = authorizationResult;

	/// <summary>
	/// Gets or sets a value indicating whether this was a cache hit.
	/// </summary>
	/// <value>The current <see cref="CacheHit"/> value.</value>
	public bool CacheHit { get; set; } = cacheHit;

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage => ProblemDetails?.Detail;

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => AuthorizationResult;

	/// <summary>
	/// Creates a successful message result.
	/// </summary>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <param name="cacheHit"> Whether this was a cache hit. </param>
	/// <returns> A successful message result. </returns>
	public static MessageResult Success(
			RoutingDecision? routingDecision = null,
			IValidationResult? validationResult = null,
			IAuthorizationResult? authorizationResult = null,
			bool cacheHit = false)
			=> new(succeeded: true, problemDetails: null, routingDecision, validationResult, authorizationResult, cacheHit);

	/// <summary>
	/// Creates a failed message result.
	/// </summary>
	/// <param name="problemDetails"> The problem details. </param>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <returns> A failed message result. </returns>
	public static MessageResult Failure(
			IMessageProblemDetails problemDetails,
			RoutingDecision? routingDecision = null,
			IValidationResult? validationResult = null,
			IAuthorizationResult? authorizationResult = null)
			=> new(succeeded: false, problemDetails, routingDecision, validationResult, authorizationResult);

	/// <summary>
	/// Creates a failed message result.
	/// </summary>
	/// <param name="problemDetails"> The problem details. </param>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <returns> A failed message result. </returns>
	public static MessageResult Failed(
			IMessageProblemDetails problemDetails,
			RoutingDecision? routingDecision = null,
			IValidationResult? validationResult = null,
			IAuthorizationResult? authorizationResult = null)
			=> Failure(problemDetails, routingDecision, validationResult, authorizationResult);

	/// <summary>
	/// Creates a cancelled message result.
	/// </summary>
	/// <returns> A cancelled message result. </returns>
	public static MessageResult Cancelled()
		=> new(succeeded: false, problemDetails: null);
}
