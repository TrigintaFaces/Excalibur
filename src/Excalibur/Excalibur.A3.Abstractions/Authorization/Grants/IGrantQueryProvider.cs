// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Extended query operations for authorization grants.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface contains advanced query methods that go beyond basic CRUD operations.
/// Access via <see cref="IGrantRequestProvider.GetService(Type)"/> with
/// <c>typeof(IGrantQueryProvider)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> Extracted from <see cref="IGrantRequestProvider"/>
/// to keep the core interface at or below 5 methods per the Microsoft-First Design Standard.
/// </para>
/// </remarks>
public interface IGrantQueryProvider
{
	/// <summary>
	/// Retrieves grants matching the provided filter.
	/// </summary>
	/// <param name="userId">Optional subject id.</param>
	/// <param name="tenantId">Tenant id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="qualifier">Qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching grants.</returns>
	Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string? userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken);

	/// <summary>
	/// Finds grants keyed by a provider-specific shape (opaque map).
	/// </summary>
	/// <remarks>
	/// This method returns an opaque dictionary to allow provider-specific projections where needed.
	/// Prefer strongly-typed methods when possible.
	/// </remarks>
	/// <param name="userId">Subject id.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Provider-specific projection of grants.</returns>
	Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken);
}
