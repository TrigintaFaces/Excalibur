// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using MongoDB.Driver;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// MongoDB implementation of the opaque <see cref="IInboxTransactionScope"/>, wrapping the
/// <see cref="IClientSessionHandle"/> whose active transaction enlists an inbox handler's own writes together
/// with the processed-mark.
/// </summary>
internal sealed class MongoInboxTransactionScope : IInboxTransactionScope
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MongoInboxTransactionScope"/> class.
	/// </summary>
	/// <param name="session">The client session handle with an active transaction.</param>
	public MongoInboxTransactionScope(IClientSessionHandle session)
	{
		ArgumentNullException.ThrowIfNull(session);
		Session = session;
	}

	/// <summary>
	/// Gets the MongoDB client session handle carrying the active transaction. Handler writes issued with this
	/// session commit atomically with the processed-mark.
	/// </summary>
	public IClientSessionHandle Session { get; }
}
