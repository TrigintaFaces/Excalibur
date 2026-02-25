// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// EventBridge-specific retry policy.
/// </summary>
public sealed class EventBridgeRetryPolicy
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaximumRetryAttempts { get; set; } = 2;

	/// <summary>
	/// Gets or sets the maximum event age in seconds.
	/// </summary>
	/// <value>
	/// The maximum event age in seconds.
	/// </value>
	public int MaximumEventAge { get; set; } = 3600;
}
