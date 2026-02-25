// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// A test saga state implementation for use in conformance tests.
/// </summary>
/// <remarks>
/// This class provides a concrete implementation of <see cref="SagaState"/> with
/// additional properties for testing state persistence, updates, and round-trip behavior.
/// </remarks>
public sealed class TestSagaState : SagaState
{
	/// <summary>
	/// Gets or sets the current status of the saga workflow.
	/// </summary>
	public string Status { get; set; } = "Pending";

	/// <summary>
	/// Gets or sets a counter value for testing state updates.
	/// </summary>
	public int Counter { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when the saga was created.
	/// </summary>
	public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the optional UTC timestamp when the saga completed.
	/// </summary>
	public DateTimeOffset? CompletedUtc { get; set; }

	/// <summary>
	/// Gets or sets additional data for the saga.
	/// </summary>
	public Dictionary<string, string> Data { get; set; } = [];

	/// <summary>
	/// Creates a test saga state with the specified saga ID.
	/// </summary>
	/// <param name="sagaId">The unique saga identifier.</param>
	/// <returns>A new test saga state instance.</returns>
	public static TestSagaState Create(Guid sagaId) =>
		new()
		{
			SagaId = sagaId,
			Status = "Created",
			Counter = 0,
			CreatedUtc = DateTimeOffset.UtcNow
		};
}
