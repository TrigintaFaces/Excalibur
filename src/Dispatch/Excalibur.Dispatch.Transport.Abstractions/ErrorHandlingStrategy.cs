// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines error handling strategies for message consumption.
/// </summary>
public enum ErrorHandlingStrategy
{
	/// <summary>
	/// Retry the message according to configured retry policy.
	/// </summary>
	Retry = 0,

	/// <summary>
	/// Move the message to dead letter queue immediately.
	/// </summary>
	DeadLetter = 1,

	/// <summary>
	/// Ignore the error and continue processing.
	/// </summary>
	Ignore = 2,

	/// <summary>
	/// Stop processing and throw the exception.
	/// </summary>
	Throw = 3,
}
