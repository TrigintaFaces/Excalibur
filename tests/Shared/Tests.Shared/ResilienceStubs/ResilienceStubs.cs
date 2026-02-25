// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.ResilienceStubs;

/// <summary>Retry policy factory interface stub for integration tests.</summary>
public interface IRetryPolicyFactory
{
	/// <summary>Creates a retry policy.</summary>
	IRetryPolicy Create(RetryPolicyOptions options);
}

/// <summary>Retry policy interface stub.</summary>
public interface IRetryPolicy
{
	/// <summary>Executes an action with retry.</summary>
	Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

	/// <summary>Executes a function with retry.</summary>
	Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);
}

/// <summary>Retry policy options stub.</summary>
public class RetryPolicyOptions
{
	/// <summary>Gets or sets the max retry attempts.</summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>Gets or sets the delay between retries.</summary>
	public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>Gets or sets whether to use exponential backoff.</summary>
	public bool UseExponentialBackoff { get; set; } = true;
}

/// <summary>Light mode deduplicator interface stub.</summary>
public interface ILightModeDeduplicator
{
	/// <summary>Checks if a message ID has been processed.</summary>
	Task<bool> HasBeenProcessedAsync(string messageId, CancellationToken cancellationToken = default);

	/// <summary>Marks a message ID as processed.</summary>
	Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <summary>Message acknowledger interface stub.</summary>
public interface IMessageAcknowledger
{
	/// <summary>Acknowledges a message.</summary>
	Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

	/// <summary>Rejects a message.</summary>
	Task RejectAsync(string messageId, bool requeue = false, CancellationToken cancellationToken = default);
}
