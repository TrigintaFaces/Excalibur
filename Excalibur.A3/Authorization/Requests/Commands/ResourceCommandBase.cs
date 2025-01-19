using System.Diagnostics;

using MediatR;

namespace Excalibur.A3.Authorization.Requests.Commands;

/// <summary>
///     An abstract base command class that provides implementations of various required interfaces.
/// </summary>
/// <typeparam name="TResourceType"> The type of resource secured by this command. </typeparam>
/// <remarks> Inherit from this base class when the command does NOT return a response. </remarks>
public abstract class ResourceCommandBase<TResourceType> : ResourceCommandBase<TResourceType, Unit>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceCommandBase{TResourceType}" /> class.
	/// </summary>
	protected ResourceCommandBase()
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceCommandBase{TResourceType}" /> class.
	/// </summary>
	/// <param name="correlationId"> A unique correlation identifier provided by the client. </param>
	/// <param name="resourceId"> The resource identifier accessed by this command. </param>
	/// <param name="tenantId"> The tenant identifier. </param>
	protected ResourceCommandBase(Guid correlationId, string resourceId, string? tenantId = null)
		: base(correlationId, resourceId, tenantId)
	{
	}
}

/// <summary>
///     An abstract base command class that provides implementations of various required interfaces.
/// </summary>
/// <typeparam name="TResourceType"> The type of resource secured by this command. </typeparam>
/// <typeparam name="TResponse"> The response type returned by the command. </typeparam>
/// <remarks> Inherit from this base class when the command returns a response. </remarks>
public abstract class ResourceCommandBase<TResourceType, TResponse> : AuthorizeCommandBase<TResponse>, IAmAuthorizableForResource
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceCommandBase{TResourceType, TResponse}" /> class.
	/// </summary>
	protected ResourceCommandBase()
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ResourceCommandBase{TResourceType, TResponse}" /> class.
	/// </summary>
	/// <param name="correlationId"> A unique correlation identifier provided by the client. </param>
	/// <param name="resourceId"> The resource identifier accessed by this command. </param>
	/// <param name="tenantId"> The tenant identifier. </param>
	protected ResourceCommandBase(Guid correlationId, string resourceId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
		ResourceId = resourceId;
	}

	/// <inheritdoc />
	public string ResourceId { get; protected init; }

	/// <inheritdoc />
	public virtual string[] ResourceTypes => [TypeNameHelper.GetTypeDisplayName(typeof(TResourceType), false, true)];
}
