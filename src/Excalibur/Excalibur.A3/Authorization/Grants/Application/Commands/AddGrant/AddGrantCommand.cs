// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a command to add a grant to a user.
/// </summary>
/// <remarks> This command is used to assign new permissions to a user by specifying the grant type, qualifier, and other details. </remarks>
/// <param name="userId"> The ID of the user to whom the grant will be added. </param>
/// <param name="fullName"> The full name of the user. </param>
/// <param name="grantType"> The type of the grant to be added. </param>
/// <param name="qualifier"> A specific qualifier for the grant, such as a resource or context. </param>
/// <param name="expiresOn"> The expiration date and time for the grant, if any. </param>
/// <param name="correlationId"> The unique correlation ID for the command. </param>
/// <param name="tenantId"> The tenant ID associated with the command, if applicable. </param>
[Activity("Add grant", "Grants a user new permissions.")]
public sealed class AddGrantCommand(
	string userId,
	string fullName,
	string grantType,
	string qualifier,
	DateTimeOffset? expiresOn,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>
	/// Gets or sets the ID of the user to whom the grant will be added.
	/// </summary>
	/// <value>The ID of the user.</value>
	public string UserId { get; set; } = userId;

	/// <summary>
	/// Gets or sets the full name of the user to whom the grant will be added.
	/// </summary>
	/// <value>The full name of the user.</value>
	public string FullName { get; set; } = fullName;

	/// <summary>
	/// Gets or sets the type of the grant to be added.
	/// </summary>
	/// <value>The type of the grant.</value>
	/// <example> Examples of grant types include "ActivityGroup" or "Activity". </example>
	public string GrantType { get; set; } = grantType;

	/// <summary>
	/// Gets or sets the qualifier for the grant.
	/// </summary>
	/// <value>The qualifier for the grant.</value>
	/// <remarks> The qualifier can be used to specify a resource or context that the grant applies Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </remarks>
	public string Qualifier { get; set; } = qualifier;

	/// <summary>
	/// Gets or sets the expiration date and time for the grant.
	/// </summary>
	/// <value>The expiration date and time, or <see langword="null"/> if the grant does not expire.</value>
	/// <remarks> If <c> null </c>, the grant will not have an expiration. </remarks>
	public DateTimeOffset? ExpiresOn { get; set; } = expiresOn;

}
