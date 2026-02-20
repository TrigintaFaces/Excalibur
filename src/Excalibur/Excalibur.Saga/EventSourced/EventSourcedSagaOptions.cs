// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// Configuration options for event-sourced saga state persistence.
/// </summary>
public sealed class EventSourcedSagaOptions
{
	/// <summary>
	/// Gets or sets the number of events after which a snapshot is taken.
	/// </summary>
	/// <value>
	/// The snapshot interval. A value of 0 disables snapshots.
	/// Default is 50.
	/// </value>
	[Range(0, int.MaxValue)]
	public int SnapshotInterval { get; set; } = 50;

	/// <summary>
	/// Gets or sets the prefix for event stream names.
	/// </summary>
	/// <value>
	/// The stream prefix. Default is "saga-".
	/// </value>
	/// <remarks>
	/// The full stream name is formed as "{StreamPrefix}{SagaId}".
	/// </remarks>
	public string StreamPrefix { get; set; } = "saga-";
}
