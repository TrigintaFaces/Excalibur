// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// A connected queue manager and its unit of work: opening a queue, and committing or backing out the
/// syncpoint the sender and receiver operate under.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the managed-client queue-manager type connects in its constructor, so a seam that
/// hands one back can only reach a live queue manager or throw. Every operation the transport performs on
/// a connection is named here instead, which is what lets the receive path be exercised without a broker.
/// </para>
/// <para>
/// <b>What is abstracted is what holds a connection, not what carries data.</b> The message and its
/// get/put option objects stay concrete: they are constructed in process, they reach no network, and
/// wrapping them would add a mapping layer on the hot path in exchange for nothing.
/// </para>
/// <para>
/// Disposing disconnects. The transport creates one of these per unit of work and always disposes it,
/// including on cancellation and on failure.
/// </para>
/// </remarks>
internal interface IIbmMqQueueManager : IDisposable
{
	/// <summary>
	/// Opens a queue on this connection.
	/// </summary>
	/// <param name="queueName">The queue to open.</param>
	/// <param name="openOptions">The MQ open options controlling input/output access.</param>
	/// <returns>The opened queue, which the caller disposes.</returns>
	IIbmMqQueue AccessQueue(string queueName, int openOptions);

	/// <summary>Commits the current unit of work.</summary>
	void Commit();

	/// <summary>Backs out the current unit of work, redelivering anything got under it.</summary>
	void Backout();
}
