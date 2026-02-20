// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base class for transport sender decorators that delegate to an inner sender.
/// Follows the <c>DelegatingChatClient</c> pattern from Microsoft.Extensions.AI — all virtual methods forward to the inner sender.
/// Subclasses override specific methods to add behavior (telemetry, ordering, deduplication, etc.).
/// </summary>
public abstract class DelegatingTransportSender : ITransportSender
{
	/// <summary>
	/// Gets the inner sender that this decorator delegates to.
	/// </summary>
	/// <value>The inner <see cref="ITransportSender"/>.</value>
	protected ITransportSender InnerSender { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to delegate to.</param>
	protected DelegatingTransportSender(ITransportSender innerSender) =>
		InnerSender = innerSender ?? throw new ArgumentNullException(nameof(innerSender));

	/// <inheritdoc />
	public virtual string Destination => InnerSender.Destination;

	/// <inheritdoc />
	public virtual Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken) =>
		InnerSender.SendAsync(message, cancellationToken);

	/// <inheritdoc />
	public virtual Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken) =>
		InnerSender.SendBatchAsync(messages, cancellationToken);

	/// <inheritdoc />
	public virtual Task FlushAsync(CancellationToken cancellationToken) =>
		InnerSender.FlushAsync(cancellationToken);

	/// <inheritdoc />
	public virtual object? GetService(Type serviceType) =>
		InnerSender.GetService(serviceType);

	/// <inheritdoc />
	public virtual ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return InnerSender.DisposeAsync();
	}
}
