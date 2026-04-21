// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using GdprCompliance.Domain.Events;

using Microsoft.Extensions.Logging;

namespace GdprCompliance.Projections;

/// <summary>
/// Projects <see cref="CustomerErasedEvent"/> into the
/// <see cref="CustomerPrivacyView"/> read model.
/// </summary>
public sealed class CustomerErasedProjectionHandler : IEventHandler<CustomerErasedEvent>
{
	private readonly ICustomerPrivacyViewStore _store;
	private readonly ILogger<CustomerErasedProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerErasedProjectionHandler"/> class.
	/// </summary>
	public CustomerErasedProjectionHandler(
		ICustomerPrivacyViewStore store,
		ILogger<CustomerErasedProjectionHandler> logger)
	{
		_store = store;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task HandleAsync(CustomerErasedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		var view = await _store.GetAsync(eventMessage.CustomerId, cancellationToken).ConfigureAwait(false)
			?? new CustomerPrivacyView { CustomerId = eventMessage.CustomerId };

		view.LastErasureRequestId = eventMessage.RequestId;
		view.LastErasureStatus = eventMessage.Status;
		view.ScheduledExecutionTime = eventMessage.ScheduledExecutionTime;
		view.LastEventAt = eventMessage.OccurredAt;
		view.Pattern = eventMessage.Pattern;

		await _store.UpsertAsync(view, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Projected CustomerErasedEvent for {CustomerId} (request {RequestId})",
			eventMessage.CustomerId,
			eventMessage.RequestId);
	}
}

/// <summary>
/// Projects <see cref="CustomerTombstonedEvent"/> into the
/// <see cref="CustomerPrivacyView"/> read model.
/// </summary>
public sealed class CustomerTombstonedProjectionHandler : IEventHandler<CustomerTombstonedEvent>
{
	private readonly ICustomerPrivacyViewStore _store;
	private readonly ILogger<CustomerTombstonedProjectionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerTombstonedProjectionHandler"/> class.
	/// </summary>
	public CustomerTombstonedProjectionHandler(
		ICustomerPrivacyViewStore store,
		ILogger<CustomerTombstonedProjectionHandler> logger)
	{
		_store = store;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task HandleAsync(CustomerTombstonedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		var view = await _store.GetAsync(eventMessage.CustomerId, cancellationToken).ConfigureAwait(false)
			?? new CustomerPrivacyView { CustomerId = eventMessage.CustomerId };

		view.LastErasureRequestId = eventMessage.RequestId;
		view.LastErasureStatus = eventMessage.Status;
		view.ScheduledExecutionTime = eventMessage.ScheduledExecutionTime;
		view.LastEventAt = eventMessage.OccurredAt;
		view.Pattern = "tombstone";

		await _store.UpsertAsync(view, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Projected CustomerTombstonedEvent for {CustomerId} (request {RequestId})",
			eventMessage.CustomerId,
			eventMessage.RequestId);
	}
}
