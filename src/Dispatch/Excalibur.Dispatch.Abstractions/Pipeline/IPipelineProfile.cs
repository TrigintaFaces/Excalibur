// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

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
	/// Gets the ordered list of middleware entries that compose this profile, each carrying whether the built pipeline may omit it.
	/// </summary>
	/// <value> The middleware entries that compose the profile. </value>
	/// <remarks>
	/// This is the profile's only middleware declaration. It deliberately replaces a bare type list rather than supplementing one: two
	/// collections describing the same middleware can disagree, and a consumer of the weaker one — such as a startup check confirming an
	/// authorization middleware is present — would then be reading a list the pipeline builder never consulted.
	/// </remarks>
	IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

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
}

/// <summary>
/// Provides message compatibility matching for pipeline profiles.
/// </summary>
public interface IPipelineProfileMatcher
{
	/// <summary>
	/// Validates whether a message is compatible with this profile.
	/// </summary>
	/// <param name="message"> The dispatch message to validate. </param>
	/// <returns> <see langword="true" /> if the message is compatible with this profile; otherwise, <see langword="false" />. </returns>
	bool IsCompatible(IDispatchMessage message);
}
