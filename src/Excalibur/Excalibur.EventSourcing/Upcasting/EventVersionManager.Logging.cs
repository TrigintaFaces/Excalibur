// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Upcasting;

public partial class EventVersionManager
{
	// Source-generated logging methods

	[LoggerMessage(EventSourcingEventId.EventUpgraderRegistered, LogLevel.Information,
		"Registered event upgrader for {EventType} from version {FromVersion} to {ToVersion}")]
	private partial void LogUpgraderRegistered(string eventType, int fromVersion, int toVersion);

	[LoggerMessage(EventSourcingEventId.EventUpgrading, LogLevel.Debug,
		"Upgrading {EventType} from version {FromVersion} to {ToVersion}")]
	private partial void LogEventUpgrading(string eventType, int fromVersion, int toVersion);
}
