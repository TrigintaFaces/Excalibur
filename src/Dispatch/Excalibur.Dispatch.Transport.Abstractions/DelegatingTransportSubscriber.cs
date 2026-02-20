// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base class for <see cref="ITransportSubscriber"/> decorators.
/// All virtual methods forward to the inner subscriber. Subclasses override to add behavior.
/// Follows the <c>DelegatingChatClient</c> pattern from Microsoft.Extensions.AI.
/// </summary>
public abstract class DelegatingTransportSubscriber : ITransportSubscriber
{
	/// <summary>
	/// Gets the inner subscriber that this decorator wraps.
	/// </summary>
	protected ITransportSubscriber InnerSubscriber { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingTransportSubscriber"/> class.
	/// </summary>
	/// <param name="innerSubscriber">The inner subscriber to delegate to.</param>
	protected DelegatingTransportSubscriber(ITransportSubscriber innerSubscriber)
	{
		InnerSubscriber = innerSubscriber ?? throw new ArgumentNullException(nameof(innerSubscriber));
	}

	/// <inheritdoc />
	public virtual string Source => InnerSubscriber.Source;

	/// <inheritdoc />
	public virtual Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		return InnerSubscriber.SubscribeAsync(handler, cancellationToken);
	}

	/// <inheritdoc />
	public virtual object? GetService(Type serviceType) => InnerSubscriber.GetService(serviceType);

	/// <inheritdoc />
	public virtual ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return InnerSubscriber.DisposeAsync();
	}
}
