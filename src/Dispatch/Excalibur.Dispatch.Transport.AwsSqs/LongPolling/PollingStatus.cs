// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents the status of an AWS SQS long polling receiver.
/// </summary>
/// <remarks>
/// Renamed from <c>PollingStatus</c> to avoid collision with
/// <c>Excalibur.Dispatch.Transport.PollingStatus</c> which defines
/// generic polling states (Idle, Running, Paused, Stopped, Error).
/// </remarks>
public enum SqsPollingStatus
{
	/// <summary>
	/// The receiver is inactive and not polling.
	/// </summary>
	Inactive,

	/// <summary>
	/// The receiver is actively polling for messages.
	/// </summary>
	Active,

	/// <summary>
	/// The receiver is in the process of stopping.
	/// </summary>
	Stopping,

	/// <summary>
	/// The receiver encountered an error.
	/// </summary>
	Error,
}
