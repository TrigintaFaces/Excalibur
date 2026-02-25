// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Controls how <see cref="RecurringDispatchScheduler" /> handles messages scheduled in the past.
/// </summary>
public enum PastScheduleBehavior
{
	/// <summary>
	/// Scheduling a message in the past results in an exception.
	/// </summary>
	Reject = 0,

	/// <summary>
	/// Messages scheduled in the past are executed immediately.
	/// </summary>
	ExecuteImmediately = 1,
}
