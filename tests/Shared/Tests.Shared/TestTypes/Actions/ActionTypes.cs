// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Tests.Shared.TestTypes.Actions;

/// <summary>
/// Test action for unit testing action handlers without a result.
/// </summary>
public class TestAction : IDispatchAction
{
	/// <inheritdoc />
	public string MessageId { get; init; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();

	/// <inheritdoc />
	public object Body => this;

	/// <inheritdoc />
	public string MessageType => nameof(TestAction);

	/// <inheritdoc />
	public string ActivityDisplayName => "Test Action";

	/// <inheritdoc />
	public string ActivityDescription => "Test action for unit tests";
}

/// <summary>
/// Result type for test actions.
/// </summary>
public class TestResult
{
	/// <summary>
	/// Gets or sets whether the action succeeded.
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the result message.
	/// </summary>
	public string? Message { get; set; }
}

/// <summary>
/// Test action that returns a result for unit testing action handlers.
/// </summary>
public class TestActionWithResult : IDispatchAction<TestResult>
{
	/// <inheritdoc />
	public string MessageId { get; init; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();

	/// <inheritdoc />
	public object Body => this;

	/// <inheritdoc />
	public string MessageType => nameof(TestActionWithResult);

	/// <inheritdoc />
	public string ActivityDisplayName => "Test Action With Result";

	/// <inheritdoc />
	public string ActivityDescription => "Test action with result for unit tests";
}

/// <summary>
/// Test action handler for actions without a result.
/// </summary>
public class TestActionHandler : IActionHandler<TestAction>
{
	/// <inheritdoc />
	public Task HandleAsync(TestAction action, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test action handler for actions with a result.
/// </summary>
public class TestActionWithResultHandler : IActionHandler<TestActionWithResult, TestResult>
{
	/// <inheritdoc />
	public Task<TestResult> HandleAsync(TestActionWithResult action, CancellationToken cancellationToken)
	{
		var result = new TestResult
		{
			Success = true,
			Message = "Test action handled successfully"
		};
		return Task.FromResult(result);
	}
}
