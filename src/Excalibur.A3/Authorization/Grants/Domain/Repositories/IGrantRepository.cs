using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.Domain.Repositories;

namespace Excalibur.A3.Authorization.Grants.Domain.Repositories;

/// <summary>
///     Represents a repository for managing <see cref="Grant" /> entities.
/// </summary>
public interface IGrantRepository : IAggregateRepository<Grant, string>
{
	/// <summary>
	///     Retrieves grants matching a specific <see cref="GrantScope" /> and optional user ID.
	/// </summary>
	/// <param name="scope"> The grant scope to filter results. </param>
	/// <param name="userId"> Optional user ID to narrow the search. </param>
	/// <returns> An <see cref="IEnumerable{T}" /> of <see cref="Grant" /> matching the criteria. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="scope" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="userId" /> is null or empty. </exception>
	Task<IEnumerable<Grant>> Matching(GrantScope scope, string? userId = null);

	/// <summary>
	///     Reads all grants associated with a specific user.
	/// </summary>
	/// <param name="userId"> The user ID for which to retrieve grants. </param>
	/// <returns> An <see cref="IEnumerable{T}" /> of <see cref="Grant" /> for the user. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="userId" /> is null or empty. </exception>
	Task<IEnumerable<Grant>> ReadAll(string userId);
}
