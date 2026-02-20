// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Commands;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Provides a base implementation for an authorization command with a specific response type.
/// </summary>
/// <typeparam name="TResponse"> The type of the response the command produces. </typeparam>
/// <remarks>
/// Inherits from <see cref="CommandBase{TResponse}" /> and implements <see cref="IAuthorizeCommand{TResponse}" />. This base class is
/// designed for commands that require authorization.
/// </remarks>
public abstract class AuthorizeCommandBase<TResponse> : CommandBase<TResponse>, IAuthorizeCommand<TResponse>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeCommandBase{TResponse}" /> class with the specified correlation ID and
	/// tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the command. </param>
	/// <param name="tenantId"> The tenant ID for the command. Defaults to <c> null </c>. </param>
	protected AuthorizeCommandBase(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeCommandBase{TResponse}" /> class with default values.
	/// </summary>
	protected AuthorizeCommandBase()
	{
	}

	/// <inheritdoc />
	public IAccessToken? AccessToken { get; set; }
}
