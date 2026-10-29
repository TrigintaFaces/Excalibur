// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>In-memory subscription state for development and testing.</summary>
/// <remarks>A member and its nullable checkpoint are one state; every operation observes one atomic snapshot.</remarks>
internal sealed class InMemorySubscriptionCheckpointStore : ISubscriptionCheckpointStore, IProjectionGenerationStore, IProjectionDestinationScope
{
    string IProjectionDestinationScope.Scope => "inmemory";

	private readonly Lock _gate = new();
	private readonly Dictionary<(string Subscription, string Key), SubscriptionMember> _members = [];
	private readonly Dictionary<(string Subscription, string Reader), (Guid Revision, string Key, Guid Binding)> _readers = [];

	public Task<ProjectionGeneration?> ReadGenerationAsync(string subscriptionName, string readerName, CancellationToken cancellationToken)
	{
		RelationalProjectionGenerationStore.Validate(subscriptionName, readerName);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			return Task.FromResult(_readers.TryGetValue((subscriptionName, readerName), out var pointer)
				? new ProjectionGeneration(pointer.Revision, _members[(subscriptionName, pointer.Key)]) : null);
		}
	}

	public Task<bool> TrySetGenerationAsync(string subscriptionName, string readerName, Guid? expectedRevision,
		string projectionKey, Guid bindingId, long minimumPosition, CancellationToken cancellationToken)
	{
		RelationalProjectionGenerationStore.Validate(subscriptionName, readerName);
		SubscriptionDefinitionValidation.ValidateBinding(subscriptionName, projectionKey, bindingId);
		ArgumentOutOfRangeException.ThrowIfNegative(minimumPosition);
		if (expectedRevision == Guid.Empty) { throw new ArgumentException("A revision cannot be empty.", nameof(expectedRevision)); }
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			var key = (subscriptionName, readerName);
			Guid? current = _readers.TryGetValue(key, out var pointer) ? pointer.Revision : null;
			if (current != expectedRevision || !_members.TryGetValue((subscriptionName, projectionKey), out var member)
				|| member.BindingId != bindingId || member.Position is not { } position || position < minimumPosition)
			{
				return Task.FromResult(false);
			}
			_readers[key] = (Guid.NewGuid(), projectionKey, bindingId);
			return Task.FromResult(true);
		}
	}

	public Task<bool> TryRemoveGenerationAsync(string subscriptionName, string readerName, Guid expectedRevision, CancellationToken cancellationToken)
	{
		RelationalProjectionGenerationStore.Validate(subscriptionName, readerName);
		if (expectedRevision == Guid.Empty) { throw new ArgumentException("A revision cannot be empty.", nameof(expectedRevision)); }
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			var key = (subscriptionName, readerName);
			return Task.FromResult(_readers.TryGetValue(key, out var current) && current.Revision == expectedRevision && _readers.Remove(key));
		}
	}

	public Task<SubscriptionRoster> BindSubscriptionAsync(string subscriptionName,
		IReadOnlyList<SubscriptionMember> members, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.Validate(subscriptionName, members);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			foreach (var member in members)
			{
				var key = (subscriptionName, member.ProjectionKey);
				if (_members.TryGetValue(key, out var existing))
				{
					SubscriptionDefinitionValidation.RequireMemberMatch(subscriptionName, member, existing);
				}
				else
				{
					_members.Add(key, member with { BindingId = Guid.NewGuid(), Position = null });
				}
			}
			return Task.FromResult(Snapshot(subscriptionName));
		}
	}

	public Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateName(subscriptionName, nameof(subscriptionName), 255);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate) { return Task.FromResult(Snapshot(subscriptionName)); }
	}

	public Task<bool> ReleaseSubscriptionMemberAsync(string subscriptionName, string projectionKey,
		Guid bindingId, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateBinding(subscriptionName, projectionKey, bindingId);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			var key = (subscriptionName, projectionKey);
			return Task.FromResult(_members.TryGetValue(key, out var member)
				&& member.BindingId == bindingId && !_readers.Values.Any(pointer => pointer.Binding == bindingId) && _members.Remove(key));
		}
	}

	public Task<long?> GetCheckpointAsync(string subscriptionName, string projectionKey, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateName(subscriptionName, nameof(subscriptionName), 255);
		SubscriptionDefinitionValidation.ValidateName(projectionKey, nameof(projectionKey), 512);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			return Task.FromResult(_members.TryGetValue((subscriptionName, projectionKey), out var member)
				? member.Position : null);
		}
	}

	public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(string subscriptionName, string projectionKey,
		Guid bindingId, long? expectedPosition, long newPosition, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateAdvance(subscriptionName, projectionKey, bindingId, expectedPosition, newPosition);
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			var key = (subscriptionName, projectionKey);
			if (!_members.TryGetValue(key, out var member) || member.BindingId != bindingId || member.Position != expectedPosition)
			{
				return Task.FromResult(CheckpointAdvanceOutcome.Superseded);
			}
			_members[key] = member with { Position = newPosition };
			return Task.FromResult(CheckpointAdvanceOutcome.Advanced);
		}
	}

	public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		lock (_gate)
		{
			IReadOnlyList<SubscriptionCheckpoint> result = [.. _members
				.Where(static entry => entry.Value.Position.HasValue)
				.Select(static entry => new SubscriptionCheckpoint(entry.Key.Subscription, entry.Key.Key, entry.Value.Position!.Value))];
			return Task.FromResult(result);
		}
	}

	private SubscriptionRoster Snapshot(string subscriptionName) => new(subscriptionName,
		Array.AsReadOnly(_members.Where(entry => string.Equals(entry.Key.Subscription, subscriptionName, StringComparison.Ordinal))
			.Select(static entry => entry.Value).ToArray()));
}
