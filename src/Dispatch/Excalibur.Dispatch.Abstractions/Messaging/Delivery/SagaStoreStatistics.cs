// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Aggregate statistics about the sagas in a store: counts of running and completed instances.
/// </summary>
public sealed class SagaStoreStatistics
{
	/// <summary>Gets the number of running (not-yet-completed) saga instances.</summary>
	/// <value>The running saga count.</value>
	public int RunningCount { get; init; }

	/// <summary>Gets the number of completed saga instances still retained in the store.</summary>
	/// <value>The completed saga count.</value>
	public int CompletedCount { get; init; }

	/// <summary>Gets the total number of saga instances in the store.</summary>
	/// <value>The total saga count.</value>
	public int TotalCount { get; init; }

	/// <summary>Gets the UTC instant these statistics were captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}
