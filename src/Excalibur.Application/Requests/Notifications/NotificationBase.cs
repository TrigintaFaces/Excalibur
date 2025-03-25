using System.Transactions;

namespace Excalibur.Application.Requests.Notifications;

/// <summary>
///     Provides a base implementation for notifications in the system.
/// </summary>
public abstract class NotificationBase : INotification
{
	/// <summary>
	///     Initializes a new instance of the <see cref="NotificationBase" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the notification. </param>
	/// <param name="tenantId"> The tenant ID associated with the notification. Defaults to "NotSpecified" if not provided. </param>
	protected NotificationBase(Guid correlationId, string? tenantId = null)
	{
		CorrelationId = correlationId;
		TenantId = tenantId ?? "NotSpecified";
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="NotificationBase" /> class with default values.
	/// </summary>
	protected NotificationBase() : this(Guid.Empty)
	{
	}

	/// <inheritdoc />
	public ActivityType ActivityType => ActivityType.Notification;

	/// <inheritdoc />
	public string ActivityName => GetType().Name;

	/// <inheritdoc />
	public abstract string ActivityDisplayName { get; }

	/// <inheritdoc />
	public abstract string ActivityDescription { get; }

	/// <inheritdoc />
	public Guid CorrelationId { get; protected init; }

	/// <inheritdoc />
	public string? TenantId { get; protected init; }

	/// <inheritdoc />
	public TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <inheritdoc />
	public IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <inheritdoc />
	public TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(2);
}
