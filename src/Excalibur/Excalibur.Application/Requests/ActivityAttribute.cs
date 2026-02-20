// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Application.Requests;

/// <summary>
/// Specifies a custom display name and description for an activity.
/// When applied to a class implementing <see cref="IActivity"/>,
/// the attribute values override the convention-based defaults derived from the type name.
/// </summary>
/// <example>
/// <code>
/// // Display name only (description uses convention default)
/// [Activity("Submit Order")]
/// public class PlaceOrderCommand : CommandBase { }
///
/// // Both display name and description
/// [Activity("Submit Order", "Submits a new order for processing")]
/// public class PlaceOrderCommand : CommandBase { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActivityAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityAttribute"/> class
	/// with the specified display name.
	/// </summary>
	/// <param name="displayName">The human-readable display name for the activity.</param>
	public ActivityAttribute(string displayName)
	{
		DisplayName = displayName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityAttribute"/> class
	/// with the specified display name and description.
	/// </summary>
	/// <param name="displayName">The human-readable display name for the activity.</param>
	/// <param name="description">A description of the activity's purpose.</param>
	public ActivityAttribute(string displayName, string description)
	{
		DisplayName = displayName;
		Description = description;
	}

	/// <summary>
	/// Gets the display name for the activity.
	/// </summary>
	/// <value>The human-readable display name.</value>
	public string DisplayName { get; }

	/// <summary>
	/// Gets the description for the activity, or <see langword="null"/>
	/// if only a display name was provided.
	/// </summary>
	/// <value>The activity description, or <see langword="null"/> when using convention default.</value>
	public string? Description { get; }
}
