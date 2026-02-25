// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base class for transport receiver decorators that delegate to an inner receiver.
/// Follows the <c>DelegatingChatClient</c> pattern from Microsoft.Extensions.AI — all virtual methods forward to the inner receiver.
/// Subclasses override specific methods to add behavior (telemetry, dead-letter routing, etc.).
/// </summary>
public abstract class DelegatingTransportReceiver : ITransportReceiver
{
	/// <summary>
	/// Gets the inner receiver that this decorator delegates to.
	/// </summary>
	/// <value>The inner <see cref="ITransportReceiver"/>.</value>
	protected ITransportReceiver InnerReceiver { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingTransportReceiver"/> class.
	/// </summary>
	/// <param name="innerReceiver">The inner receiver to delegate to.</param>
	protected DelegatingTransportReceiver(ITransportReceiver innerReceiver) =>
		InnerReceiver = innerReceiver ?? throw new ArgumentNullException(nameof(innerReceiver));

	/// <inheritdoc />
	public virtual string Source => InnerReceiver.Source;

	/// <inheritdoc />
	public virtual Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken) =>
		InnerReceiver.ReceiveAsync(maxMessages, cancellationToken);

	/// <inheritdoc />
	public virtual Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) =>
		InnerReceiver.AcknowledgeAsync(message, cancellationToken);

	/// <inheritdoc />
	public virtual Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) =>
		InnerReceiver.RejectAsync(message, reason, requeue, cancellationToken);

	/// <inheritdoc />
	public virtual object? GetService(Type serviceType) =>
		InnerReceiver.GetService(serviceType);

	/// <inheritdoc />
	public virtual ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return InnerReceiver.DisposeAsync();
	}
}
