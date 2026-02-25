// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Reasons why a message became poisoned/dead letter.
/// </summary>
public enum PoisonReason
{
	/// <summary>
	/// Maximum delivery attempts exceeded.
	/// </summary>
	MaxDeliveryAttemptsExceeded = 0,

	/// <summary>
	/// Message processing timeout.
	/// </summary>
	ProcessingTimeout = 1,

	/// <summary>
	/// Unhandled exception during processing.
	/// </summary>
	UnhandledException = 2,

	/// <summary>
	/// Message format is invalid.
	/// </summary>
	InvalidFormat = 3,

	/// <summary>
	/// Message is too old.
	/// </summary>
	MessageExpired = 4,

	/// <summary>
	/// Unknown reason.
	/// </summary>
	Unknown = 99,
}
