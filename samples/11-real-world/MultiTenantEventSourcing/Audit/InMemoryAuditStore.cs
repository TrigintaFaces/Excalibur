// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Audit.Events;

namespace MultiTenantEventSourcing.Audit;

/// <summary>Demo in-memory store for <see cref="ActivityAudited"/> records.</summary>
public sealed class InMemoryAuditStore
{
	private readonly ConcurrentQueue<ActivityAudited> _records = new();

	public void Add(ActivityAudited record)
	{
		ArgumentNullException.ThrowIfNull(record);
		_records.Enqueue(record);
	}

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
