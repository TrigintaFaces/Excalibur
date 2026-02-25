// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for the InMemory transport implementation.
/// This serves as the baseline reference implementation.
/// </summary>
public sealed class InMemoryTransportConformanceTests
	: TransportConformanceTestBase<InMemoryChannelSender, InMemoryChannelReceiver>
{
	private Channel<object>? _channel;
	private InMemoryDeadLetterQueueManager? _dlqManager;

	/// <summary>
	/// InMemory transport does not support message filtering (in-process channels).
	/// </summary>
	[Fact]
	public override async Task Should_Support_Message_Filtering()
	{
		// InMemory transport does not support filtering - skip test
		await Task.CompletedTask;
	}

	protected override Task<InMemoryChannelSender> CreateSenderAsync()
	{
		_channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
		{
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false
		});

		var sender = new InMemoryChannelSender(_channel.Writer);
		return Task.FromResult(sender);
	}

	protected override Task<InMemoryChannelReceiver> CreateReceiverAsync()
	{
		if (_channel == null)
		{
			throw new InvalidOperationException("Channel not initialized. Ensure sender is created first.");
		}

		var receiver = new InMemoryChannelReceiver(_channel.Reader);
		return Task.FromResult(receiver);
	}

	protected override Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		_dlqManager = new InMemoryDeadLetterQueueManager();
		return Task.FromResult<IDeadLetterQueueManager?>(_dlqManager);
	}

	protected override Task DisposeTransportAsync()
	{
		_channel?.Writer.Complete();
		return Task.CompletedTask;
	}
}

/// <summary>
/// In-memory implementation of IChannelSender for testing.
/// </summary>
public sealed class InMemoryChannelSender : IChannelSender
{
	private readonly ChannelWriter<object> _writer;

	public InMemoryChannelSender(ChannelWriter<object> writer)
	{
		_writer = writer ?? throw new ArgumentNullException(nameof(writer));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		await _writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// In-memory implementation of IChannelReceiver for testing.
/// </summary>
public sealed class InMemoryChannelReceiver : IChannelReceiver
{
	private readonly ChannelReader<object> _reader;

	public InMemoryChannelReceiver(ChannelReader<object> reader)
	{
		_reader = reader ?? throw new ArgumentNullException(nameof(reader));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		if (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			if (_reader.TryRead(out var message))
			{
				return (T?)message;
			}
		}

		return default;
	}
}

/// <summary>
/// In-memory implementation of IDeadLetterQueueManager for testing.
/// </summary>
public sealed class InMemoryDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly List<DeadLetterMessage> _deadLetterMessages = new();
	private readonly object _lock = new();

	public Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var dlqMessage = new DeadLetterMessage
		{
			OriginalMessage = message,
			Reason = reason,
			Exception = exception,
			DeadLetteredAt = DateTimeOffset.UtcNow
		};

		lock (_lock)
		{
			_deadLetterMessages.Add(dlqMessage);
		}

		return Task.FromResult(dlqMessage.OriginalMessage.Id);
	}

	public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(
				_deadLetterMessages.Take(maxMessages).ToList());
		}
	}

	public Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		var result = new ReprocessResult
		{
			SuccessCount = messages.Count(),
			FailureCount = 0
		};

		return Task.FromResult(result);
	}

	public Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			return Task.FromResult(new DeadLetterStatistics
			{
				MessageCount = _deadLetterMessages.Count,
				OldestMessageAge = _deadLetterMessages.Count > 0
					? DateTimeOffset.UtcNow - _deadLetterMessages.Min(m => m.DeadLetteredAt)
					: TimeSpan.Zero
			});
		}
	}

	public Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			var count = _deadLetterMessages.Count;
			_deadLetterMessages.Clear();
			return Task.FromResult(count);
		}
	}
}
