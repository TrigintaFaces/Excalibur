// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing.Policies;

/// <summary>
/// Configuration options for external routing policy file loading.
/// </summary>
public sealed class RoutingPolicyOptions
{
	/// <summary>
	/// Gets or sets the file path to the routing policy JSON file.
	/// </summary>
	/// <value>The file path, or <see langword="null"/> to disable file-based routing.</value>
	public string? PolicyFilePath { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to watch the policy file for changes.
	/// </summary>
	/// <value><see langword="true"/> to enable hot-reload; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool WatchForChanges { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to throw on missing file.
	/// If false, an empty rule set is used.
	/// </summary>
	/// <value><see langword="true"/> to throw; <see langword="false"/> to use empty rules. Defaults to <see langword="false"/>.</value>
	public bool ThrowOnMissingFile { get; set; }
}
