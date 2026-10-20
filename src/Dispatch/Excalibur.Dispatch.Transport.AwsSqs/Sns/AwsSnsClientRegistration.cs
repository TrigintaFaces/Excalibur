// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SimpleNotificationService;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Owns only the lazy send client belonging to an identified framework management client.</summary>
internal sealed class AwsSnsClientRegistration : IDisposable
{
	private readonly Lock _gate = new();
	private IAmazonSimpleNotificationService? _management;
	private IAmazonSimpleNotificationService? _send;
	private Func<IAmazonSimpleNotificationService>? _createSend;
	private volatile bool _disposed;

	internal void Register(IAmazonSimpleNotificationService management, Func<IAmazonSimpleNotificationService> createSend)
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			_management = management;
			_createSend = createSend;
		}
	}

	internal IAmazonSimpleNotificationService Resolve(IAmazonSimpleNotificationService actual)
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (!ReferenceEquals(actual, _management)) { return actual; }
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
