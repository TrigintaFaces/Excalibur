using Excalibur.Application.Requests.Commands;

using MediatR;

namespace Excalibur.A3.Authorization.Requests.Commands;

/// <summary>
///     Provides a base implementation for an authorization command with no response.
/// </summary>
/// <remarks>
///     Inherits from <see cref="AuthorizeCommandBase{Unit}" /> and is designed for scenarios where the command does not require a response object.
/// </remarks>
public abstract class AuthorizeCommandBase : AuthorizeCommandBase<Unit>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeCommandBase" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the command. </param>
	/// <param name="tenantId"> The tenant ID for the command. Defaults to <c> null </c>. </param>
	protected AuthorizeCommandBase(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeCommandBase" /> class with default values.
	/// </summary>
	protected AuthorizeCommandBase()
	{
	}
}

/// <summary>
///     Provides a base implementation for an authorization command with a specific response type.
/// </summary>
/// <typeparam name="TResponse"> The type of the response the command produces. </typeparam>
/// <remarks>
///     Inherits from <see cref="CommandBase{TResponse}" /> and implements <see cref="IAuthorizeCommand{TResponse}" />. This base class is
///     designed for commands that require authorization.
/// </remarks>
public abstract class AuthorizeCommandBase<TResponse> : CommandBase<TResponse>, IAuthorizeCommand<TResponse>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeCommandBase{TResponse}" /> class with the specified correlation ID and
	///     tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the command. </param>
	/// <param name="tenantId"> The tenant ID for the command. Defaults to <c> null </c>. </param>
	protected AuthorizeCommandBase(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeCommandBase{TResponse}" /> class with default values.
	/// </summary>
	protected AuthorizeCommandBase()
	{
	}

	/// <inheritdoc />
	public IAccessToken AccessToken { get; set; }
}
