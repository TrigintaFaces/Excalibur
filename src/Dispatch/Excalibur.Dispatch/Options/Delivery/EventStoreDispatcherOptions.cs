// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Options controlling the <see cref="EventStoreDispatcherService" /> polling behaviour.
/// </summary>
public sealed class EventStoreDispatcherOptions
{
	/// <summary>
	/// Gets or sets how often the dispatcher checks for undispatched events.
	/// </summary>
	/// <value>
	/// How often the dispatcher checks for undispatched events.
	/// </value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);
}
