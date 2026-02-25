// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Notifications;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Provides a base implementation for notifications that require authorization.
/// </summary>
/// <remarks>
/// Implements the <see cref="IAuthorizeNotification" /> interface, ensuring that notifications can enforce access control policies and
/// handle authorization tokens.
/// </remarks>
public abstract class AuthorizeNotificationBase : NotificationBase, IAuthorizeNotification
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeNotificationBase" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to track the notification. </param>
	/// <param name="tenantId"> The tenant ID associated with the notification. Defaults to <c> null </c>. </param>
	protected AuthorizeNotificationBase(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizeNotificationBase" /> class with default values.
	/// </summary>
	protected AuthorizeNotificationBase()
	{
	}

	/// <inheritdoc />
	public IAccessToken? AccessToken { get; set; }
}
