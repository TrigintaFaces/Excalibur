// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines the contract for long polling strategies that adapt wait times based on message patterns.
/// </summary>
public interface ILongPollingStrategy
{
	/// <summary>
	/// Gets the strategy name.
	/// </summary>
	/// <value>
	/// The strategy name.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Executes the polling strategy.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message being polled. </typeparam>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<List<TMessage>> PollAsync<TMessage>(string queueUrl, CancellationToken cancellationToken)
		where TMessage : class;

	/// <summary>
	/// Calculates the optimal wait time based on current message flow patterns.
	/// </summary>
	/// <returns> The recommended wait time for the next receive operation. </returns>
	ValueTask<TimeSpan> CalculateOptimalWaitTimeAsync();

	/// <summary>
	/// Records the result of a receive operation to update strategy calculations.
	/// </summary>
	/// <param name="messageCount"> The number of messages received. </param>
	/// <param name="actualWaitTime"> The actual time spent waiting. </param>
	ValueTask RecordReceiveResultAsync(int messageCount, TimeSpan actualWaitTime);

	/// <summary>
	/// Gets the current load factor (0.0 to 1.0) based on recent message patterns.
	/// </summary>
	/// <returns> The current load factor where 1.0 represents maximum load. </returns>
	ValueTask<double> GetCurrentLoadFactorAsync();

	/// <summary>
	/// Resets the strategy's internal state and statistics.
	/// </summary>
	ValueTask ResetAsync();

	/// <summary>
	/// Gets statistics about the strategy's performance.
	/// </summary>
	/// <returns> The current strategy statistics. </returns>
	ValueTask<LongPollingStatistics> GetStatisticsAsync();
}
