// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Default <see cref="IServiceBusProcessorSeam"/> implementation that forwards
/// to a real <see cref="ServiceBusProcessor"/>. This adapter is the only place
/// in the transport subscriber path that touches the live Azure Service Bus SDK
/// processor type — tests substitute at the seam, never at the SDK type
/// directly (ADR-142 §D7).
/// </summary>
internal sealed class ServiceBusProcessorAdapter : IServiceBusProcessorSeam
{
	private readonly ServiceBusProcessor _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusProcessorAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Azure Service Bus processor.</param>
	public ServiceBusProcessorAdapter(ServiceBusProcessor inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync
	{
		add => _inner.ProcessMessageAsync += value;
		remove => _inner.ProcessMessageAsync -= value;
	}

	/// <inheritdoc/>
	public event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync
	{
		add => _inner.ProcessErrorAsync += value;
		remove => _inner.ProcessErrorAsync -= value;
	}

	/// <inheritdoc/>
	public Task StartProcessingAsync(CancellationToken cancellationToken)
		=> _inner.StartProcessingAsync(cancellationToken);

	/// <inheritdoc/>
	public Task StopProcessingAsync(CancellationToken cancellationToken)
		=> _inner.StopProcessingAsync(cancellationToken);

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
		=> _inner.DisposeAsync();
}
