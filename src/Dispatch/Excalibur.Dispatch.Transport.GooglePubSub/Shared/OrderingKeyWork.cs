// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents work to be processed with an ordering key.
/// </summary>
public sealed class OrderingKeyWork
{
	/// <summary>
	/// Gets or sets the ordering key.
	/// </summary>
	/// <value>
	/// The ordering key.
	/// </value>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the work item to process.
	/// </summary>
	/// <value>
	/// The work item to process.
	/// </value>
	public Func<Task> WorkItem { get; set; } = static () => Task.CompletedTask;
}
