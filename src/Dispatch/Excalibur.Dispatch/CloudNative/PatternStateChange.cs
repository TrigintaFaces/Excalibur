// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Represents a state change in a pattern.
/// </summary>
public sealed class PatternStateChange
{
	/// <summary>
	/// Gets or sets when the state change occurred.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets previous state.
	/// </summary>
	/// <value>The current <see cref="PreviousState"/> value.</value>
	public object? PreviousState { get; set; }

	/// <summary>
	/// Gets or sets new state.
	/// </summary>
	/// <value>The current <see cref="NewState"/> value.</value>
	public object? NewState { get; set; }

	/// <summary>
	/// Gets or sets reason for the state change.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets additional context about the state change.
	/// </summary>
	/// <value>The current <see cref="Context"/> value.</value>
	public Dictionary<string, object> Context { get; init; } = [];
}
