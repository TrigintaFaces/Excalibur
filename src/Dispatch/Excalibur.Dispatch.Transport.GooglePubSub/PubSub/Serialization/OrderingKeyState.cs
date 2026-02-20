// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents the state of an ordering key.
/// </summary>
public sealed record OrderingKeyState(
	string OrderingKey,
	bool IsPaused,
	int PendingCount,
	DateTimeOffset? LastProcessedTime,
	string? LastMessageId = null);
