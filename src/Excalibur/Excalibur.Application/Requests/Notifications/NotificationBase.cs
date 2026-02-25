// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Transactions;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Notifications;

/// <summary>
/// Provides a base implementation for notifications in the system.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NotificationBase" /> class with the specified correlation ID and tenant ID.
/// </remarks>
/// <param name="correlationId"> The correlation ID for the notification. </param>
/// <param name="tenantId"> The tenant ID associated with the notification. Defaults to TenantDefaults.DefaultTenantId if not provided. </param>
public abstract class NotificationBase(Guid correlationId, string? tenantId = null) : INotification
{
	private readonly Dictionary<string, object> _headers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="NotificationBase" /> class with default values.
	/// </summary>
	protected NotificationBase()
		: this(Guid.Empty)
	{
	}

	/// <summary>
	/// Gets the unique identifier for this notification as a GUID.
	/// </summary>
	/// <value> A unique identifier for this notification instance. </value>
	public Guid Id { get; protected init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the unique identifier for this notification as a string.
	/// </summary>
	/// <value> The string representation of the notification's unique identifier. </value>
	public string MessageId => Id.ToString();

	/// <summary>
	/// Gets the type identifier for this notification.
	/// </summary>
	/// <value> The fully qualified type name of the notification. </value>
	public string MessageType => GetType().FullName ?? GetType().Name;

	/// <summary>
	/// Gets the kind of message this notification represents.
	/// </summary>
	/// <value> Always returns <see cref="MessageKinds.Event" /> for notifications. </value>
	public MessageKinds Kind { get; protected init; } = MessageKinds.Event;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> A read-only dictionary containing the notification's metadata headers. </value>
	public IReadOnlyDictionary<string, object> Headers => new ReadOnlyDictionary<string, object>(_headers);

	/// <summary>
	/// Gets the type of activity this notification represents.
	/// </summary>
	/// <value> Always returns <see cref="ActivityType.Notification" /> for notifications. </value>
	public ActivityType ActivityType => ActivityType.Notification;

	/// <summary>
	/// Gets the name of this activity.
	/// </summary>
	/// <value> The namespace-qualified type name (e.g., <c>MyApp.Orders:OrderShippedNotification</c>). </value>
	public string ActivityName => ActivityNameConvention.ResolveName(GetType());

	/// <summary>
	/// Gets the display name for this activity.
	/// </summary>
	/// <value> A human-readable name for display purposes. </value>
	public virtual string ActivityDisplayName => ActivityNameConvention.ResolveDisplayName(GetType());

	/// <summary>
	/// Gets the description of this activity.
	/// </summary>
	/// <value> A detailed description of what this activity represents. </value>
	public virtual string ActivityDescription => ActivityNameConvention.ResolveDescription(GetType());

	/// <summary>
	/// Gets the correlation identifier for this notification.
	/// </summary>
	/// <value> A unique identifier used to correlate this notification with other operations. </value>
	public Guid CorrelationId { get; protected init; } = correlationId;

	/// <summary>
	/// Gets the tenant identifier associated with this notification.
	/// </summary>
	/// <value> The tenant identifier, or TenantDefaults.DefaultTenantId if no tenant ID was provided. </value>
	public string? TenantId { get; protected init; } = tenantId ?? TenantDefaults.DefaultTenantId;

	/// <summary>
	/// Gets the transaction behavior for processing this notification.
	/// </summary>
	/// <value> The transaction scope option. Defaults to <see cref="TransactionScopeOption.Required" />. </value>
	public TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <summary>
	/// Gets the transaction isolation level for processing this notification.
	/// </summary>
	/// <value> The isolation level. Defaults to <see cref="IsolationLevel.ReadCommitted" />. </value>
	public IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <summary>
	/// Gets the transaction timeout for processing this notification.
	/// </summary>
	/// <value> The maximum time allowed for the transaction. Defaults to 2 minutes. </value>
	public TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(2);
}
