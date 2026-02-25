// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Information about a checkpoint.
/// </summary>
public sealed class CheckpointInfo
{
	/// <summary>
	/// Gets or sets the checkpoint identifier.
	/// </summary>
	/// <value>The current <see cref="CheckpointId"/> value.</value>
	public string CheckpointId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string SessionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the checkpoint was created.
	/// </summary>
	/// <value>The current <see cref="CreatedAt"/> value.</value>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the size of the checkpoint data.
	/// </summary>
	/// <value>The current <see cref="SizeInBytes"/> value.</value>
	public long SizeInBytes { get; set; }

	/// <summary>
	/// Gets checkpoint metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; init; } = [];
}
