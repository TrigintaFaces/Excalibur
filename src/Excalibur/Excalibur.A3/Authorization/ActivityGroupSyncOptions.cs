// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Configures how activity-group data is synchronized from the remote authority.
/// </summary>
public sealed class ActivityGroupSyncOptions
{
	/// <summary>
	/// Gets or sets what a grant sync does when the configured grant store cannot replace a set of grants
	/// atomically.
	/// </summary>
	/// <value>
	/// The default is <see cref="GrantSyncAtomicity.Required"/>, which refuses such a store at start-up. A
	/// store that can replace atomically ignores this setting.
	/// </value>
	public GrantSyncAtomicity GrantSyncAtomicity { get; set; } = GrantSyncAtomicity.Required;
}
