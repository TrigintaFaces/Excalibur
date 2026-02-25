// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Provides context for saga execution.
/// </summary>
/// <typeparam name="TSagaData"> The type of data flowing through the saga. </typeparam>
public interface ISagaContext<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets the saga identifier.
	/// </summary>
	/// <value> The identifier that correlates saga operations. </value>
	string SagaId { get; }

	/// <summary>
	/// Gets or sets the saga data.
	/// </summary>
	/// <value> The mutable saga data payload. </value>
	TSagaData Data { get; set; }

	/// <summary>
	/// Gets the current step index.
	/// </summary>
	/// <value> The zero-based index of the active saga step. </value>
	int CurrentStepIndex { get; }

	/// <summary>
	/// Gets metadata associated with the saga.
	/// </summary>
	/// <value> A metadata dictionary scoped to the saga. </value>
	IDictionary<string, object> Metadata { get; }

	/// <summary>
	/// Gets the activity log.
	/// </summary>
	/// <value> The read-only activity log. </value>
	IReadOnlyList<SagaActivity> Activities { get; }

	/// <summary>
	/// Adds an activity log entry.
	/// </summary>
	/// <param name="message"> The log message. </param>
	/// <param name="details"> Additional details. </param>
	void AddActivity(string message, object? details = null);
}
