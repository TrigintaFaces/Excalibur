// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Metadata associated with a message batch.
/// </summary>
public sealed class BatchMetadata
{
	/// <summary>
	/// Gets or sets the pull duration in milliseconds.
	/// </summary>
	/// <value>
	/// The pull duration in milliseconds.
	/// </value>
	public double PullDurationMs { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether flow control was applied.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether flow control was applied.
	/// </value>
	public bool FlowControlApplied { get; set; }

	/// <summary>
	/// Gets or sets the effective batch size after flow control.
	/// </summary>
	/// <value>
	/// The effective batch size after flow control.
	/// </value>
	public int EffectiveBatchSize { get; set; }

	/// <summary>
	/// Gets or sets custom properties.
	/// </summary>
	/// <value>
	/// Custom properties.
	/// </value>
	public Dictionary<string, object> Properties { get; set; } = [];
}
