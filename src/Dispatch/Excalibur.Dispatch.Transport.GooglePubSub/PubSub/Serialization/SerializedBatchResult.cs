// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents the serializable result of batch processing for JSON contexts.
/// </summary>
public sealed record SerializedBatchResult(
	List<ProcessedMessage> Processed,
	List<FailedMessage> Failed,
	TimeSpan TotalDuration,
	Dictionary<string, object>? Metrics = null);
