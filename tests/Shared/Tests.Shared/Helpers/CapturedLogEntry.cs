// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Helpers;

/// <summary>
/// Represents a captured log entry for test assertions.
/// </summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event ID.</param>
/// <param name="Message">The formatted log message.</param>
/// <param name="Exception">The exception, if any.</param>
/// <param name="CategoryName">The logger category name.</param>
public sealed record CapturedLogEntry(
	LogLevel Level,
	EventId EventId,
	string Message,
	Exception? Exception,
	string CategoryName = "");
