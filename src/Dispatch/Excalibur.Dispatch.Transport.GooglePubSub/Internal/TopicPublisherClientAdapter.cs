// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Default <see cref="ITopicPublisherClientSeam"/> implementation that forwards to a real
/// high-level <see cref="PublisherClient"/>. This adapter is the only place in the bus publish path
/// that touches the live Pub/Sub SDK client type — tests substitute at the seam, never at the SDK
/// type directly.
/// </summary>
internal sealed class TopicPublisherClientAdapter : ITopicPublisherClientSeam, IAsyncDisposable
{
	private readonly PublisherClient _inner;
	private readonly bool _ownsClient;
	private readonly Lock _disposeGate = new();
	private Task? _disposeTask;

	/// <summary>
	/// Initializes a new instance of the <see cref="TopicPublisherClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying high-level Pub/Sub publisher client.</param>
	/// <param name="ownsClient">Whether this adapter owns the client's asynchronous shutdown.</param>
	public TopicPublisherClientAdapter(PublisherClient inner, bool ownsClient = false)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
		_ownsClient = ownsClient;
	}

	/// <inheritdoc/>
	public Task<string> PublishAsync(PubsubMessage message) => _inner.PublishAsync(message);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (!_ownsClient)
		{
			return ValueTask.CompletedTask;
		}
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
		try
		{
			await _inner.DisposeAsync().ConfigureAwait(false);
			completion.TrySetResult();
		}
		catch (OperationCanceledException exception)
		{
			completion.TrySetCanceled(exception.CancellationToken);
		}
		catch (Exception exception)
		{
			completion.TrySetException(exception);
		}
	}
}
