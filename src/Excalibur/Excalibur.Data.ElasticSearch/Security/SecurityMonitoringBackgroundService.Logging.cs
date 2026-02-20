// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

internal sealed partial class SecurityMonitoringBackgroundService
{
	// Source-generated logging methods

	[LoggerMessage(DataElasticsearchEventId.SecurityMonitoringStarted, LogLevel.Information,
		"Security monitoring background service started")]
	private partial void LogStarted();

	[LoggerMessage(DataElasticsearchEventId.SecurityMonitoringStopped, LogLevel.Information,
		"Security monitoring background service stopped")]
	private partial void LogStopped();
}
