// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Fake user repository for testing.
/// </summary>
public class FakeUserRepository
{
	private readonly Dictionary<string, FakeUser> users = [];

	/// <summary>
	///     Gets a user by MessageId.
	/// </summary>
	public Task<FakeUser?> GetByIdAsync(string Id, CancellationToken cancellationToken = default)
	{
		_ = users.TryGetValue(Id, out var user);
		return Task.FromResult(user);
	}

	/// <summary>
	///     Gets all users.
	/// </summary>
	public Task<IEnumerable<FakeUser>> GetAllAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(users.Values.AsEnumerable());

	/// <summary>
	///     Adds a user.
	/// </summary>
	public Task AddAsync(FakeUser user, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(user);

		users[user.Id] = user;
		return Task.CompletedTask;
	}

	/// <summary>
	///     Updates a user.
	/// </summary>
	public Task UpdateAsync(FakeUser user, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(user);

		if (!users.ContainsKey(user.Id))
		{
			throw new InvalidOperationException($"User with MessageId {user.Id} not found");
		}

		users[user.Id] = user;
		return Task.CompletedTask;
	}

	/// <summary>
	///     Deletes a user.
	/// </summary>
	public Task DeleteAsync(string Id, CancellationToken cancellationToken = default)
	{
		_ = users.Remove(Id);
		return Task.CompletedTask;
	}

	/// <summary>
	///     Clears all users.
	/// </summary>
	public void Clear() => users.Clear();
}
