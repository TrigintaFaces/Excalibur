// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
/// Represents a collection of activities and their associated data.
/// </summary>
/// <param name="activities"> A collection of activities. </param>
internal sealed class Activities(IEnumerable<IActivity> activities)
{
	/// <summary>
	/// Gets or sets the dictionary containing activity data, indexed by activity name.
	/// </summary>
	/// <value>The dictionary containing activity data.</value>
	public IDictionary<string, Data> Value { get; set; } = activities.ToDictionary(
		static a => a.ActivityName,
		static a => new Data(((IRequireActivityAuthorization)a).ResourceTypes), StringComparer.Ordinal);

	/// <summary>
	/// Represents data associated with an activity.
	/// </summary>
	internal sealed record Data
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Data"/> class.
		/// Initializes a new instance of the <see cref="Data" /> record.
		/// </summary>
		/// <param name="resourceTypes"> A list of resource types associated with the activity. </param>
		public Data(IList<string>? resourceTypes)
		{
			if (resourceTypes?.Any() ?? false)
			{
				ResourceTypes = resourceTypes;
			}
		}

		/// <summary>
		/// Gets or sets the resource types associated with the activity.
		/// </summary>
		/// <value>The resource types associated with the activity.</value>
		public IList<string> ResourceTypes { get; set; } = [];
	}
}
