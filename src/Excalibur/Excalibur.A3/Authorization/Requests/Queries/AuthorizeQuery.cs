// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Queries;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Provides a base implementation for queries that require authorization.
/// </summary>
/// <typeparam name="TResponse"> The type of response produced by the query. </typeparam>
/// <remarks>
/// Implements the <see cref="IAuthorizeQuery{TResponse}" /> interface, ensuring that queries can enforce access control policies and
/// handle authorization tokens.
/// </remarks>
public abstract class AuthorizeQuery<TResponse> : QueryBase<TResponse>, IAuthorizeQuery<TResponse>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeQuery{TResponse}" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to track the query. </param>
	/// <param name="tenantId"> The tenant ID associated with the query. Defaults to <c> null </c>. </param>
	protected AuthorizeQuery(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeQuery{TResponse}" /> class with default values.
	/// </summary>
	protected AuthorizeQuery()
	{
	}

	/// <inheritdoc />
	public IAccessToken? AccessToken { get; set; }
}
