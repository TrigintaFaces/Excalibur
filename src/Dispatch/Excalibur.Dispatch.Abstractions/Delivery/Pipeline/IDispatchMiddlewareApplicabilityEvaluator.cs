// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines methods for evaluating middleware applicability based on message kinds and features. Implements requirements R2.4-R2.6.
/// </summary>
public interface IDispatchMiddlewareApplicabilityEvaluator
{
	/// <summary>
	/// Determines whether a middleware type is applicable for the specified message kind.
	/// </summary>
	/// <param name="middlewareType"> The middleware type to evaluate. </param>
	/// <param name="messageKind"> The message kind to check against. </param>
	/// <returns> true if the middleware is applicable; otherwise, false. </returns>
	/// <remarks>
	/// This method evaluates R2.4 (applicability via attributes or interface) and R2.5 (negative applicability). Exclusion attributes
	/// always override inclusion attributes.
	/// </remarks>
	bool IsApplicable(Type middlewareType, MessageKinds messageKind);

	/// <summary>
	/// Determines whether a middleware type is applicable for the specified message kind and enabled features.
	/// </summary>
	/// <param name="middlewareType"> The middleware type to evaluate. </param>
	/// <param name="messageKind"> The message kind to check against. </param>
	/// <param name="enabledFeatures"> The set of enabled dispatch features. </param>
	/// <returns> true if the middleware is applicable and all required features are enabled; otherwise, false. </returns>
	/// <remarks>
	/// This method evaluates all requirements: R2.4 (applicability), R2.5 (exclusions), and R2.6 (feature requirements). Pipeline
	/// synthesizer uses this method to omit middleware requiring disabled features.
	/// </remarks>
	bool IsApplicable(Type middlewareType, MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures);

	/// <summary>
	/// Determines whether a middleware instance is applicable for the specified message kind.
	/// </summary>
	/// <param name="middleware"> The middleware instance to evaluate. </param>
	/// <param name="messageKind"> The message kind to check against. </param>
	/// <returns> true if the middleware is applicable; otherwise, false. </returns>
	/// <remarks>
	/// This method prefers attribute-based configuration over the ApplicableMessageKinds interface property for performance and consistency.
	/// </remarks>
	bool IsApplicable(IDispatchMiddleware middleware, MessageKinds messageKind);

	/// <summary>
	/// Filters a collection of middleware types, returning only those applicable for the specified criteria.
	/// </summary>
	/// <param name="middlewareTypes"> The middleware types to filter. </param>
	/// <param name="messageKind"> The message kind to check against. </param>
	/// <param name="enabledFeatures"> The optional set of enabled dispatch features. </param>
	/// <returns> An enumerable of middleware types that are applicable. </returns>
	/// <remarks>
	/// This is the primary method used by the pipeline to filter middleware during message dispatch. If enabledFeatures is null, only
	/// message kind applicability is evaluated.
	/// </remarks>
	IEnumerable<Type> FilterApplicableMiddleware(
		IEnumerable<Type> middlewareTypes,
		MessageKinds messageKind,
		IReadOnlySet<DispatchFeatures>? enabledFeatures = null);
}
