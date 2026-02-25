// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Generic message result with a return value.
/// </summary>
/// <typeparam name="T"> The type of the return value. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="MessageResultOfT{T}" /> class. </remarks>
/// <param name="succeeded"> Whether the dispatch succeeded. </param>
/// <param name="returnValue"> The return value. </param>
/// <param name="problemDetails"> Problem details if failed. </param>
/// <param name="routingDecision"> Routing result. </param>
/// <param name="validationResult"> Validation result. </param>
/// <param name="authorizationResult"> Authorization result. </param>
/// <param name="cacheHit"> Whether this was a cache hit. </param>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class MessageResultOfT<T>(
		bool succeeded,
		T? returnValue,
		IMessageProblemDetails? problemDetails = null,
		RoutingDecision? routingDecision = null,
		IValidationResult? validationResult = null,
		IAuthorizationResult? authorizationResult = null,
		bool cacheHit = false) : MessageResult(succeeded, problemDetails, routingDecision, validationResult, authorizationResult, cacheHit),
		IMessageResult<T>
{
	/// <summary>
	/// Gets or sets the return value.
	/// </summary>
	/// <value>The current <see cref="ReturnValue"/> value.</value>
	public T? ReturnValue { get; set; } = returnValue;

	/// <summary>
	/// Creates a successful message result with a return value.
	/// </summary>
	/// <param name="returnValue"> The return value. </param>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <param name="cacheHit"> Whether this was a cache hit. </param>
	/// <returns> A successful message result. </returns>
	public static MessageResultOfT<T> Success(
			T? returnValue,
			RoutingDecision? routingDecision = null,
			IValidationResult? validationResult = null,
			IAuthorizationResult? authorizationResult = null,
			bool cacheHit = false)
			=> new(succeeded: true, returnValue, problemDetails: null, routingDecision, validationResult, authorizationResult, cacheHit);

	/// <summary>
	/// Creates a failed message result.
	/// </summary>
	/// <param name="problemDetails"> The problem details. </param>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <returns> A failed message result. </returns>
	public static new MessageResultOfT<T> Failure(
			IMessageProblemDetails problemDetails,
			RoutingDecision? routingDecision = null,
			IValidationResult? validationResult = null,
			IAuthorizationResult? authorizationResult = null)
			=> new(succeeded: false, default, problemDetails, routingDecision, validationResult, authorizationResult);

	/// <summary>
	/// Creates a cancelled message result.
	/// </summary>
	/// <returns> A cancelled message result. </returns>
	public static new MessageResultOfT<T> Cancelled()
		=> new(succeeded: false, default, problemDetails: null);
}
