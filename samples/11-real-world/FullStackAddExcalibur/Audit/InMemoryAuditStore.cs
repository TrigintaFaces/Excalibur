// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Audit.Events;

namespace FullStackAddExcalibur.Audit;

/// <summary>
/// Demo in-memory store for <see cref="ActivityAudited"/> records emitted by
/// <see cref="Excalibur.A3.Audit.AuditMiddleware"/>.
/// </summary>
/// <remarks>
/// A production deployment routes the published audit events to Kafka, SQL
/// Server, ElasticSearch, Splunk, Datadog, etc. This store keeps the sample
/// runnable end-to-end without extra infrastructure.
/// </remarks>
public sealed class InMemoryAuditStore
{
	private readonly ConcurrentQueue<ActivityAudited> _records = new();

	/// <summary>Appends an audit record to the store.</summary>
	public void Add(ActivityAudited record)
	{
		ArgumentNullException.ThrowIfNull(record);
		_records.Enqueue(record);
	}

	/// <summary>
	/// Returns the most-recent <paramref name="take"/> audit records in
	/// insertion order (newest last).
	/// </summary>
	public IReadOnlyList<ActivityAudited> TakeRecent(int take)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

		var snapshot = _records.ToArray();
		if (snapshot.Length <= take)
		{
			return snapshot;
		}

		var tail = new ActivityAudited[take];
		Array.Copy(snapshot, snapshot.Length - take, tail, 0, take);
		return tail;
	}
}
