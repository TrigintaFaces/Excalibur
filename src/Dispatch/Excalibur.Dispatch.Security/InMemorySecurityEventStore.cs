// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;



namespace Excalibur.Dispatch.Security;

/// <summary>
/// In-memory implementation of <see cref="ISecurityEventStore"/> for development and testing scenarios.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed class InMemorySecurityEventStore : ISecurityEventStore
{
	private readonly List<SecurityEvent> _events = [];

	/// <inheritdoc/>
	public Task StoreEventsAsync(IEnumerable<SecurityEvent> events, CancellationToken cancellationToken)
	{
		_events.AddRange(events);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IEnumerable<SecurityEvent>> QueryEventsAsync(SecurityEventQuery query, CancellationToken cancellationToken)
	{
		var results = _events.AsEnumerable();

		if (query.StartTime.HasValue)
		{
			results = results.Where(e => e.Timestamp >= query.StartTime.Value);
		}

		if (query.EndTime.HasValue)
		{
			results = results.Where(e => e.Timestamp <= query.EndTime.Value);
		}

		if (query.EventType.HasValue)
		{
			results = results.Where(e => e.EventType == query.EventType.Value);
		}

		return Task.FromResult(results.Take(query.MaxResults));
	}
}
