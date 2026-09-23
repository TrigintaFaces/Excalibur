// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ITransportSender"/>.
/// Sends messages via gRPC unary calls to a remote dispatch transport server.
/// </summary>
internal sealed partial class GrpcTransportSender : ITransportSender
{
	private readonly GrpcChannel? _channel;
	private readonly CallInvoker _invoker;
	private readonly GrpcTransportOptions _options;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportSender"/> class.
	/// </summary>
	/// <param name="channel">The gRPC channel.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	public GrpcTransportSender(
		GrpcChannel channel,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportSender> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_invoker = _channel.CreateCallInvoker();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportSender"/> class with an explicit
	/// <see cref="CallInvoker"/> (the gRPC injection seam) instead of a channel, matching the seam the
	/// receiver and subscriber already expose. Used to substitute a fake invoker under test so the batch
	/// response — the part this transport must not trust — can be driven without a live server. There is
	/// no owned channel on this path, so <see cref="GetService(Type)"/> returns <see langword="null"/> for
	/// <see cref="GrpcChannel"/> and disposal has no channel to release.
	/// </summary>
	/// <param name="invoker">The gRPC call invoker that issues send RPCs.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	internal GrpcTransportSender(
		CallInvoker invoker,
		IOptions<GrpcTransportOptions> options,
		ILogger<GrpcTransportSender> logger)
	{
		_invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_channel = null;
	}

	/// <inheritdoc />
	public string Destination => _options.Destination;

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var request = MapToRequest(message);
			var method = GrpcMethodDescriptors.CreateSendMethod(_options.SendMethodPath);
			var callOptions = CreateCallOptions(cancellationToken);

			var response = await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

			if (response.IsSuccess)
			{
				LogMessageSent(message.Id, Destination);
				return SendResult.Success(response.MessageId ?? message.Id);
			}

			return SendResult.Failure(new SendError
			{
				Code = response.ErrorCode ?? "GrpcError",
				Message = response.ErrorMessage ?? "Unknown gRPC error",
				IsRetryable = false,
			});
		}
		catch (RpcException ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(SendError.FromException(ex, IsTransient(ex)));
		}
	}

	/// <inheritdoc />
	public async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		if (messages.Count == 0)
		{
			return new BatchSendResult { TotalMessages = 0, SuccessCount = 0, FailureCount = 0 };
		}

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			var request = new GrpcBatchRequest
			{
				Messages = messages.Select(MapToRequest).ToList(),
			};

			var method = GrpcMethodDescriptors.CreateSendBatchMethod(_options.SendBatchMethodPath);
			var callOptions = CreateCallOptions(cancellationToken);

			var response = await _invoker.AsyncUnaryCall(method, null, callOptions, request)
				.ConfigureAwait(false);

			// THE RESPONSE IS NOT TRUSTED TO DESCRIBE THE REQUEST UNTIL IT IS CHECKED.
			// response.Results is a remote server's list, of whatever length and order that server chose.
			// Mapping it straight through while computing the counts from the CALLER's list produced a
			// BatchSendResult whose Results did not correspond to the inputs, whose FailureCount was
			// arithmetic across two different bases, and which looked entirely well-formed to the caller.
			if (response.Results.Count != messages.Count)
			{
				LogBatchResultCountMismatch(Destination, messages.Count, response.Results.Count);

				// Every input gets an entry carrying its OWN identity, because the one thing we now know
				// is that we cannot say which inputs were sent. Reported as retryable and explicitly NOT
				// as "not sent": the server may have accepted some, so a retry can duplicate. That is
				// consistent with the at-least-once guarantee this transport offers, and a duplicate is
				// recoverable where a silent loss is not.
				var mismatch = new SendError
				{
					Code = "GrpcBatchResultCountMismatch",
					Message = $"The server returned {response.Results.Count} results for a batch of "
						+ $"{messages.Count} messages, so no result can be attributed to an input. Some "
						+ "messages may have been sent.",
					IsRetryable = true,
				};

				return new BatchSendResult
				{
					TotalMessages = messages.Count,
					SuccessCount = 0,
					FailureCount = messages.Count,
					Results = [.. messages.Select(m => new SendResult
					{
						IsSuccess = false,
						MessageId = m.Id,
						Error = mismatch,
					})],
					Duration = stopwatch.Elapsed,
				};
			}

			// Bound by the check above, so entry i describes messages[i]. The input's identity is stamped
			// rather than the server's: a caller needs to know WHICH OF ITS OWN messages an entry is
			// about, and the server's id can be absent entirely.
			var results = new SendResult[messages.Count];
			for (var i = 0; i < messages.Count; i++)
			{
				var r = response.Results[i];
				results[i] = r.IsSuccess
					? new SendResult
					{
						IsSuccess = true,
						MessageId = messages[i].Id,
						SequenceNumber = null,
					}
					: new SendResult
					{
						IsSuccess = false,
						MessageId = messages[i].Id,
						Error = new SendError
						{
							Code = r.ErrorCode ?? "GrpcError",
							Message = r.ErrorMessage ?? "Unknown error",
						},
					};
			}

			var successCount = results.Count(static r => r.IsSuccess);
			LogBatchSent(Destination, messages.Count, successCount);

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = successCount,
				// Every count now describes the SAME list.
				FailureCount = results.Length - successCount,
				Results = results,
				Duration = stopwatch.Elapsed,
			};
		}
		catch (RpcException ex)
		{
			LogBatchSendFailed(Destination, messages.Count, ex);

			// Each failure carries its own input identity. SendResult.Failure(SendError) leaves MessageId
			// null, which left a caller unable to say which input a failed entry belonged to.
			var error = SendError.FromException(ex, IsTransient(ex));
			var failedResults = messages
				.Select(m => new SendResult { IsSuccess = false, MessageId = m.Id, Error = error })
				.ToList();

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = 0,
				FailureCount = messages.Count,
				Results = failedResults,
				Duration = stopwatch.Elapsed,
			};
		}
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		// gRPC calls are immediately committed; no buffering to flush.
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(GrpcChannel))
		{
			return _channel;
		}

		return null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_channel?.Dispose();
		LogDisposed(Destination);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private static GrpcTransportRequest MapToRequest(TransportMessage message) =>
		new()
		{
			Id = message.Id,
			Body = Convert.ToBase64String(message.Body.Span),
			ContentType = message.ContentType,
			MessageType = message.MessageType,
			CorrelationId = message.CorrelationId,
			Subject = message.Subject,
			Destination = message.HasProperties
				? message.Properties.GetValueOrDefault(GrpcTransportPropertyKeys.Destination) as string
				: null,
			Properties = message.HasProperties
				? message.Properties.Where(kv => kv.Value is string)
					.ToDictionary(kv => kv.Key, kv => (string)kv.Value)
				: [],
		};

	private CallOptions CreateCallOptions(CancellationToken cancellationToken) =>
		new(
			deadline: DateTime.UtcNow.AddSeconds(_options.DeadlineSeconds),
			cancellationToken: cancellationToken);

	private static bool IsTransient(RpcException ex) =>
		ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Aborted;

	[LoggerMessage(GrpcTransportEventId.SenderMessageSent, LogLevel.Debug,
		"gRPC transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(GrpcTransportEventId.SenderSendFailed, LogLevel.Error,
		"gRPC transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(GrpcTransportEventId.SenderBatchSent, LogLevel.Debug,
		"gRPC transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(GrpcTransportEventId.SenderBatchSendFailed, LogLevel.Error,
		"gRPC transport sender: batch send of {Count} messages to {Destination} failed")]
	private partial void LogBatchSendFailed(string destination, int count, Exception exception);

	[LoggerMessage(GrpcTransportEventId.SenderBatchResultCountMismatch, LogLevel.Error,
		"gRPC batch to {Destination}: sent {SentCount} messages and the server returned "
		+ "{ReturnedCount} results, so no result can be attributed to an input.")]
	private partial void LogBatchResultCountMismatch(string destination, int sentCount, int returnedCount);

	[LoggerMessage(GrpcTransportEventId.SenderDisposed, LogLevel.Debug,
		"gRPC transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
