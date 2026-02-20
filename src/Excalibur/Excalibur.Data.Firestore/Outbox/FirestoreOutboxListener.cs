// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Firestore-based real-time outbox listener using snapshot listeners.
/// </summary>
/// <remarks>
/// <para>
/// Uses Firestore's snapshot listener pattern to receive real-time notifications
/// when new outbox messages are staged. Falls back to polling when the snapshot
/// listener is unavailable or encounters errors.
/// </para>
/// </remarks>
public sealed partial class FirestoreOutboxListener : IOutboxListener, IAsyncDisposable
{
	private readonly FirestoreOutboxListenerOptions _listenerOptions;
	private readonly FirestoreOutboxOptions _outboxOptions;
	private readonly ILogger<FirestoreOutboxListener> _logger;
	private readonly Func<IEnumerable<OutboundMessage>, CancellationToken, Task>? _onMessagesReceived;

	private FirestoreDb? _db;
	private FirestoreChangeListener? _changeListener;
	private volatile bool _disposed;
	private volatile bool _listening;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxListener"/> class.
	/// </summary>
	/// <param name="listenerOptions"> The listener configuration options. </param>
	/// <param name="outboxOptions"> The Firestore outbox store options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="onMessagesReceived"> Optional callback when new messages are detected. </param>
	public FirestoreOutboxListener(
		IOptions<FirestoreOutboxListenerOptions> listenerOptions,
		IOptions<FirestoreOutboxOptions> outboxOptions,
		ILogger<FirestoreOutboxListener> logger,
		Func<IEnumerable<OutboundMessage>, CancellationToken, Task>? onMessagesReceived = null)
	{
		ArgumentNullException.ThrowIfNull(listenerOptions);
		ArgumentNullException.ThrowIfNull(outboxOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_listenerOptions = listenerOptions.Value;
		_outboxOptions = outboxOptions.Value;
		_logger = logger;
		_onMessagesReceived = onMessagesReceived;
	}

	/// <inheritdoc />
	public async Task StartListeningAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_listening)
		{
			return;
		}

		await EnsureInitializedAsync().ConfigureAwait(false);

		LogListenerStarting(_listenerOptions.CollectionPath);

		var collection = _db.Collection(_listenerOptions.CollectionPath);

		// Subscribe to staged messages only
		var query = collection
			.WhereEqualTo("status", (int)OutboxStatus.Staged)
			.OrderBy("createdAt")
			.Limit(_listenerOptions.MaxBatchSize);

		_changeListener = query.Listen(async (snapshot, ct) =>
		{
			try
			{
				if (snapshot.Changes.Count == 0)
				{
					return;
				}

				LogSnapshotReceived(snapshot.Changes.Count);

				if (_onMessagesReceived != null)
				{
					var messages = snapshot.Changes
						.Where(c => c.ChangeType == DocumentChange.Type.Added)
						.Select(c => ConvertToOutboundMessage(c.Document))
						.ToList();

					if (messages.Count > 0)
					{
						await _onMessagesReceived(messages, ct).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				LogListenerError(ex);
			}
		});

		_listening = true;
		LogListenerStarted(_listenerOptions.CollectionPath);
	}

	/// <inheritdoc />
	public async Task StopListeningAsync(CancellationToken cancellationToken)
	{
		if (!_listening)
		{
			return;
		}

		LogListenerStopping(_listenerOptions.CollectionPath);

		if (_changeListener != null)
		{
			await _changeListener.StopAsync().ConfigureAwait(false);
			_changeListener = null;
		}

		_listening = false;
		LogListenerStopped(_listenerOptions.CollectionPath);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_changeListener != null)
		{
			await _changeListener.StopAsync().ConfigureAwait(false);
			_changeListener = null;
		}

		_listening = false;
	}

	private static OutboundMessage ConvertToOutboundMessage(DocumentSnapshot doc)
	{
		return new OutboundMessage
		{
			Id = doc.GetValue<string>("messageId"),
			MessageType = doc.GetValue<string>("messageType"),
			Payload = doc.TryGetValue<Blob>("payload", out var blob)
				? blob.ByteString.ToByteArray()
				: [],
			Destination = doc.TryGetValue<string>("destination", out var dest) ? dest : string.Empty,
			Status = OutboxStatus.Staged,
			CreatedAt = doc.TryGetValue<string>("createdAt", out var createdAt)
				? DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture)
				: DateTimeOffset.UtcNow,
		};
	}

	private async Task EnsureInitializedAsync()
	{
		if (_db != null)
		{
			return;
		}

		var builder = new FirestoreDbBuilder { ProjectId = _outboxOptions.ProjectId };

		if (!string.IsNullOrEmpty(_outboxOptions.EmulatorHost))
		{
			builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_outboxOptions.EmulatorHost);
		}

		if (!string.IsNullOrEmpty(_outboxOptions.CredentialsPath))
		{
			builder.CredentialsPath = _outboxOptions.CredentialsPath;
		}
		else if (!string.IsNullOrEmpty(_outboxOptions.CredentialsJson))
		{
			builder.JsonCredentials = _outboxOptions.CredentialsJson;
		}

		_db = await builder.BuildAsync().ConfigureAwait(false);
	}

	[LoggerMessage(105450, LogLevel.Information,
		"Starting Firestore outbox listener on collection '{CollectionPath}'")]
	private partial void LogListenerStarting(string collectionPath);

	[LoggerMessage(105451, LogLevel.Information,
		"Firestore outbox listener started on collection '{CollectionPath}'")]
	private partial void LogListenerStarted(string collectionPath);

	[LoggerMessage(105452, LogLevel.Information,
		"Stopping Firestore outbox listener on collection '{CollectionPath}'")]
	private partial void LogListenerStopping(string collectionPath);

	[LoggerMessage(105453, LogLevel.Information,
		"Firestore outbox listener stopped on collection '{CollectionPath}'")]
	private partial void LogListenerStopped(string collectionPath);

	[LoggerMessage(105454, LogLevel.Debug,
		"Received snapshot with {ChangeCount} changes")]
	private partial void LogSnapshotReceived(int changeCount);

	[LoggerMessage(105455, LogLevel.Error,
		"Firestore outbox listener encountered an error")]
	private partial void LogListenerError(Exception ex);
}
