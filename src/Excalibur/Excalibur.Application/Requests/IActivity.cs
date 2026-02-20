// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Application.Requests;

/// <summary>
/// Represents an activity that is correlatable, multi-tenant, and transactional.
/// </summary>
public interface IActivity : IAmCorrelatable, IAmMultiTenant
{
	/// <summary>
	/// Gets the type of the activity.
	/// </summary>
	/// <value>
	/// The type of the activity.
	/// </value>
	ActivityType ActivityType { get; }

	/// <summary>
	/// Gets the name of the activity.
	/// </summary>
	/// <value>
	/// The name of the activity.
	/// </value>
	[JsonIgnore]
	string ActivityName { get; }

	/// <summary>
	/// Gets the display name of the activity.
	/// </summary>
	/// <value>
	/// The display name of the activity.
	/// </value>
	[JsonIgnore]
	string ActivityDisplayName { get; }

	/// <summary>
	/// Gets the description of the activity.
	/// </summary>
	/// <value>
	/// The description of the activity.
	/// </value>
	[JsonIgnore]
	string ActivityDescription { get; }
}
