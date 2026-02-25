// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Represents a retry attempt.
/// </summary>
public sealed record RetryAttempt(
	int AttemptNumber,
	DateTimeOffset AttemptTime,
	string? Error,
	TimeSpan NextDelay);
