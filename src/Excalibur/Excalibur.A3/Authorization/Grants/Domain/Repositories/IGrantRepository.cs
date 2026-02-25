// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a repository for managing <see cref="Grant" /> entities.
/// </summary>
/// <remarks>
/// <para>
/// Extends the Excalibur.EventSourcing event-sourced repository with Grant-specific query methods.
/// </para>
/// </remarks>
public interface IGrantRepository : IEventSourcedRepository<Grant>
{
	/// <summary>
	/// Retrieves grants matching a specific <see cref="GrantScope" /> and optional user ID.
	/// </summary>
	/// <param name="scope"> The grant scope to filter results. </param>
	/// <param name="userId"> Optional user ID to narrow the search. </param>
	/// <returns> An <see cref="IEnumerable{T}" /> of <see cref="Grant" /> matching the criteria. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="scope" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="userId" /> is null or empty. </exception>
	Task<IEnumerable<Grant>> MatchingAsync(GrantScope scope, string? userId = null);

	/// <summary>
	/// Reads all grants associated with a specific user.
	/// </summary>
	/// <param name="userId"> The user ID for which to retrieve grants. </param>
	/// <returns> An <see cref="IEnumerable{T}" /> of <see cref="Grant" /> for the user. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="userId" /> is null or empty. </exception>
	Task<IEnumerable<Grant>> ReadAllAsync(string userId);
}
