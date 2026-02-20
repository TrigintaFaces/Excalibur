// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Implements a fixed long polling strategy that uses a constant wait time.
/// </summary>
public sealed class FixedLongPollingStrategy : ILongPollingStrategy
{
	private readonly TimeSpan _fixedWaitTime;
	private long _totalReceives;
	private long _totalMessages;
	private long _emptyReceives;
	private long _apiCallsSaved;
	private DateTimeOffset _lastReceiveTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="FixedLongPollingStrategy" /> class.
	/// </summary>
	/// <param name="configuration"> The long polling configuration. </param>
	public FixedLongPollingStrategy(LongPollingConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		configuration.Validate();

		_fixedWaitTime = TimeSpan.FromSeconds(configuration.MaxWaitTimeSeconds);
		_lastReceiveTime = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FixedLongPollingStrategy" /> class with a specific wait time.
	/// </summary>
	/// <param name="fixedWaitTime"> The fixed wait time to use. </param>
	public FixedLongPollingStrategy(TimeSpan fixedWaitTime)
	{
		if (fixedWaitTime < TimeSpan.Zero || fixedWaitTime > TimeSpan.FromSeconds(20))
		{
			throw new ArgumentOutOfRangeException(
				nameof(fixedWaitTime),
				"Wait time must be between 0 and 20 seconds.");
		}

		_fixedWaitTime = fixedWaitTime;
		_lastReceiveTime = DateTimeOffset.UtcNow;
	}

	/// <inheritdoc />
	public string Name => "Fixed";

	/// <inheritdoc />
	public ValueTask<TimeSpan> CalculateOptimalWaitTimeAsync() => new(_fixedWaitTime);

	/// <inheritdoc />
	public ValueTask RecordReceiveResultAsync(int messageCount, TimeSpan actualWaitTime)
	{
		_totalReceives++;
		_totalMessages += messageCount;
		if (messageCount == 0)
		{
			_emptyReceives++;
		}

		_lastReceiveTime = DateTimeOffset.UtcNow;

		// Calculate API calls saved
		if (actualWaitTime > TimeSpan.FromSeconds(1))
		{
			var potentialEmptyPolls = (int)(actualWaitTime.TotalSeconds / 1) - 1;
			if (messageCount == 0)
			{
				_apiCallsSaved += potentialEmptyPolls;
			}
		}

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<double> GetCurrentLoadFactorAsync()
	{
		// For fixed strategy, calculate load factor based on average messages
		var avgMessages = _totalReceives > 0 ? (double)_totalMessages / _totalReceives : 0;
		var loadFactor = avgMessages / 10.0; // Assuming max 10 messages per receive
		return new ValueTask<double>(Math.Min(1.0, loadFactor));
	}

	/// <inheritdoc />
	public ValueTask ResetAsync()
	{
		_totalReceives = 0;
		_totalMessages = 0;
		_emptyReceives = 0;
		_apiCallsSaved = 0;
		_lastReceiveTime = DateTimeOffset.UtcNow;
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask<LongPollingStatistics> GetStatisticsAsync()
	{
		var stats = new LongPollingStatistics
		{
			TotalReceives = _totalReceives,
			TotalMessages = _totalMessages,
			EmptyReceives = _emptyReceives,
			CurrentLoadFactor = _totalReceives > 0 ? (double)_totalMessages / _totalReceives / 10.0 : 0,
			CurrentWaitTime = _fixedWaitTime,
			ApiCallsSaved = _apiCallsSaved,
			LastReceiveTime = _lastReceiveTime,
		};

		return new ValueTask<LongPollingStatistics>(stats);
	}

	/// <inheritdoc />
	public Task<List<TMessage>> PollAsync<TMessage>(string queueUrl, CancellationToken cancellationToken)
		where TMessage : class =>

		// This strategy doesn't actually poll - it just provides wait time calculations
		// The actual polling is done by the receiver using this strategy
		throw new InvalidOperationException(
			"FixedLongPollingStrategy provides wait time calculations only. Use ILongPollingReceiver for actual polling.");
}
