// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SQS;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Owns only the lazy send client belonging to an identified framework receive client.</summary>
internal sealed class AwsSqsClientRegistration : IDisposable
{
	private readonly Lock _gate = new();
	private IAmazonSQS? _receive;
	private IAmazonSQS? _send;
	private Func<IAmazonSQS>? _createSend;
	private volatile bool _disposed;
	private int? _retries;
	private TimeSpan? _timeout;

	internal void Register(IAmazonSQS receive, Func<IAmazonSQS> createSend, int? retries = null, TimeSpan? timeout = null)
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			_receive = receive;
			_createSend = createSend;
			_retries = retries;
			_timeout = timeout;
		}
	}

	internal IAmazonSQS Resolve(IAmazonSQS actual, AwsSqsOptions options)
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (!ReferenceEquals(actual, _receive)) { return actual; }
			if (_retries is { } retries) { options.MaxRetryAttempts = retries; }
			if (_timeout is { } timeout) { options.RequestTimeout = timeout; }
			return _send ??= _createSend!();
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_disposed) { return; }
			_disposed = true;
			_send?.Dispose();
		}
	}
}
