// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Information about a saga instance for monitoring purposes.
/// </summary>
/// <param name="SagaId">The unique identifier for the saga instance.</param>
/// <param name="SagaType">The fully qualified type name of the saga.</param>
/// <param name="IsCompleted">
/// Indicates whether the saga has completed. A completed saga may have
/// succeeded or failed with compensation.
/// </param>
/// <param name="CreatedAt">The UTC timestamp when the saga was created.</param>
/// <param name="LastUpdatedAt">The UTC timestamp when the saga was last updated.</param>
/// <param name="CompletedAt">
/// The UTC timestamp when the saga completed, or <see langword="null"/> if still running.
/// </param>
/// <param name="FailureReason">
/// The failure reason if the saga failed, or <see langword="null"/> if succeeded or still running.
/// </param>
/// <remarks>
/// <para>
/// This record provides a summary view of saga state suitable for monitoring dashboards.
/// It intentionally excludes the full saga state (StateJson) to minimize data transfer.
/// </para>
/// <para>
/// To determine saga health:
/// <list type="bullet">
/// <item><description>Healthy: <c>IsCompleted == false</c> and recently updated</description></item>
/// <item><description>Stuck: <c>IsCompleted == false</c> and not updated within threshold</description></item>
/// <item><description>Failed: <c>FailureReason != null</c></description></item>
/// <item><description>Completed: <c>IsCompleted == true</c> and <c>FailureReason == null</c></description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record SagaInstanceInfo(
	Guid SagaId,
	string SagaType,
	bool IsCompleted,
	DateTime CreatedAt,
	DateTime LastUpdatedAt,
	DateTime? CompletedAt,
	string? FailureReason);
