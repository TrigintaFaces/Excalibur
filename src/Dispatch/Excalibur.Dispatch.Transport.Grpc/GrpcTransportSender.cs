// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

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
	private readonly GrpcChannel _channel;
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

		var stopwatch = Stopwatch.StartNew();

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

			stopwatch.Stop();

			var results = response.Results.Select(r => r.IsSuccess
				? SendResult.Success(r.MessageId ?? string.Empty)
				: SendResult.Failure(new SendError
				{
					Code = r.ErrorCode ?? "GrpcError",
					Message = r.ErrorMessage ?? "Unknown error",
				})).ToList();

			var successCount = results.Count(static r => r.IsSuccess);
			LogBatchSent(Destination, messages.Count, successCount);

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = successCount,
				FailureCount = messages.Count - successCount,
				Results = results,
				Duration = stopwatch.Elapsed,
			};
		}
		catch (RpcException ex)
		{
			stopwatch.Stop();
			LogBatchSendFailed(Destination, messages.Count, ex);

			var failedResults = messages.Select(_ =>
				SendResult.Failure(SendError.FromException(ex, IsTransient(ex)))).ToList();

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
		_channel.Dispose();
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
				? message.Properties.GetValueOrDefault("dispatch.destination") as string
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

	[LoggerMessage(GrpcTransportEventId.SenderDisposed, LogLevel.Debug,
		"gRPC transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
