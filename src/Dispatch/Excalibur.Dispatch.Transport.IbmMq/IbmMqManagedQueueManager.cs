// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using IBM.WMQ;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Adapts a connected managed-client queue manager to <see cref="IIbmMqQueueManager"/>.
/// </summary>
/// <remarks>
/// Every member forwards directly; the type adds no behaviour of its own. That is deliberate — the seam
/// exists so the connection can be substituted, and an adapter that reinterpreted the client's semantics
/// would make what runs under test differ from what runs in production.
/// </remarks>
internal sealed class IbmMqManagedQueueManager : IIbmMqQueueManager
{
	private readonly MQQueueManager _queueManager;

	public IbmMqManagedQueueManager(MQQueueManager queueManager)
	{
		_queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
	}

	/// <inheritdoc />
	public IIbmMqQueue AccessQueue(string queueName, int openOptions) =>
		new ManagedQueue(_queueManager.AccessQueue(queueName, openOptions));

	/// <inheritdoc />
	public void Commit() => _queueManager.Commit();

	/// <inheritdoc />
	public void Backout() => _queueManager.Backout();

	/// <inheritdoc />
	public void Dispose() => _queueManager.Disconnect();

	private sealed class ManagedQueue : IIbmMqQueue
	{
		private readonly MQQueue _queue;

		public ManagedQueue(MQQueue queue)
		{
			_queue = queue;
		}

		public void Get(MQMessage message, MQGetMessageOptions options) => _queue.Get(message, options);

		public void Put(MQMessage message, MQPutMessageOptions options) => _queue.Put(message, options);

		public void Dispose() => _queue.Close();
	}
}
