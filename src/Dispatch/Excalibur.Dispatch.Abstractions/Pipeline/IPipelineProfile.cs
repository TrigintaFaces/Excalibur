// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a reusable pipeline configuration profile that specifies which middleware to include and in what order for specific processing scenarios.
/// </summary>
public interface IPipelineProfile
{
	/// <summary>
	/// Gets the unique name of this pipeline profile.
	/// </summary>
	/// <value> The profile identifier used during pipeline selection. </value>
	string Name { get; }

	/// <summary>
	/// Gets the description of what this profile is designed for.
	/// </summary>
	/// <value> A human-readable description of the profile intent. </value>
	string Description { get; }

	/// <summary>
	/// Gets the ordered list of middleware types to include in this profile.
	/// </summary>
	/// <value> The middleware types that compose the profile. </value>
	IReadOnlyList<Type> MiddlewareTypes { get; }

	/// <summary>
	/// Gets a value indicating whether this profile enforces strict ordering and validation.
	/// </summary>
	/// <value> <see langword="true" /> when middleware ordering is enforced; otherwise, <see langword="false" />. </value>
	bool IsStrict { get; }

	/// <summary>
	/// Gets the message kinds this profile is optimized for.
	/// </summary>
	/// <value> The message kinds targeted by the profile. </value>
	MessageKinds SupportedMessageKinds { get; }

	/// <summary>
	/// Validates whether a message is compatible with this profile.
	/// </summary>
	/// <param name="message"> The dispatch message to validate. </param>
	/// <returns> <see langword="true" /> if the message is compatible with this profile; otherwise, <see langword="false" />. </returns>
	[RequiresUnreferencedCode("Uses reflection to determine message kind.")]
	bool IsCompatible(IDispatchMessage message);

	/// <summary>
	/// Gets middleware applicable to the specified message kind.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind);

	/// <summary>
	/// Gets middleware applicable to the specified message kind and enabled features. Implements R2.6.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <param name="enabledFeatures"> The set of enabled dispatch features. </param>
	/// <returns> An ordered list of applicable middleware types. </returns>
	IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind, IReadOnlySet<DispatchFeatures> enabledFeatures);
}
