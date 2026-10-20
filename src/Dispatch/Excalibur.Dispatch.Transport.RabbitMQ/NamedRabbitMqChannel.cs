// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>Owns only the SDK resources created for a named publishing channel.</summary>
internal sealed class NamedRabbitMqChannel(IChannel channel, IConnection? connection, bool ownsChannel, bool ownsConnection) : IAsyncDisposable
{
	private readonly Lock _disposeGate = new();
	private Task? _disposeTask;

	internal IChannel Channel { get; } = channel;

	public ValueTask DisposeAsync()
	{
		TaskCompletionSource? owner = null;
		Task shutdown;
		lock (_disposeGate)
		{
			if (_disposeTask is null)
			{
				owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				_disposeTask = owner.Task;
			}
			shutdown = _disposeTask;
		}
		if (owner is not null)
		{
			_ = CompleteShutdownAsync(owner);
		}
		return new ValueTask(shutdown);
	}

	private async Task CompleteShutdownAsync(TaskCompletionSource completion)
	{
		var errors = new List<Exception>();
		try
		{
			if (ownsChannel)
			{
				await Channel.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception exception)
		{
			errors.Add(exception);
		}
		try
		{
			if (ownsConnection && connection is not null)
			{
				await connection.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception exception)
		{
			errors.Add(exception);
		}
		if (errors.Count == 0)
		{
			completion.TrySetResult();
		}
		else if (errors.Count == 1 && errors[0] is OperationCanceledException canceled)
		{
			completion.TrySetCanceled(canceled.CancellationToken);
		}
		else
		{
			completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException("RabbitMQ channel and connection cleanup failed.", errors));
		}
	}
}
