// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Specifies the type of messaging destination.
/// </summary>
public enum DestinationType
{
	/// <summary>
	/// A point-to-point queue.
	/// </summary>
	Queue = 0,

	/// <summary>
	/// A publish-subscribe topic.
	/// </summary>
	Topic = 1,

	/// <summary>
	/// A subscription to a topic.
	/// </summary>
	Subscription = 2,
}
