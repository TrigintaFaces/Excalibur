// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Application.Requests.Commands;
using Excalibur.Dispatch;
using Excalibur.Compliance;

using GdprCompliance.Domain;
using GdprCompliance.Domain.Events;

using Microsoft.Extensions.Logging;

namespace GdprCompliance.Commands;

/// <summary>
/// Handles <see cref="EraseCustomerCommand"/> by filing an audit-tracked erasure
/// request, clearing every <see cref="PersonalDataAttribute"/> field in-place,
/// and dispatching a <see cref="CustomerErasedEvent"/> for downstream projections.
/// </summary>
public sealed class EraseCustomerHandler : ICommandHandler<EraseCustomerCommand, CustomerErasureResponse>
{
	private readonly ICustomerRepository _repository;
	private readonly IErasureService _erasureService;
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<EraseCustomerHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="EraseCustomerHandler"/> class.
	/// </summary>
	public EraseCustomerHandler(
		ICustomerRepository repository,
		IErasureService erasureService,
		IDispatcher dispatcher,
		ILogger<EraseCustomerHandler> logger)
	{
		_repository = repository;
		_erasureService = erasureService;
		_dispatcher = dispatcher;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<CustomerErasureResponse> HandleAsync(
		EraseCustomerCommand action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var customer = await _repository.FindAsync(action.CustomerId).ConfigureAwait(false)
			?? throw new KeyNotFoundException($"Customer {action.CustomerId} not found");

		// 1. File the audit-tracked erasure request via IErasureService.
		var request = new ErasureRequest
		{
			DataSubjectId = customer.Id.ToString("D"),
			IdType = DataSubjectIdType.UserId,
			RequestedBy = action.RequestedBy,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Scope = ErasureScope.User,
		};
		var result = await _erasureService.RequestErasureAsync(request, cancellationToken).ConfigureAwait(false);

		// 2. Erase-in-place so downstream reads cannot see PII.
		customer.FullName = string.Empty;
		customer.Email = string.Empty;
		customer.PhoneNumber = null;
		customer.NationalIdNumber = null;
		await _repository.SaveAsync(customer).ConfigureAwait(false);

		// 3. Publish the domain event so projection handlers can update read models.
		await _dispatcher
			.DispatchAsync(
				new CustomerErasedEvent(
					customer.Id,
					result.RequestId,
					result.Status,
					result.ScheduledExecutionTime,
					Pattern: "erase-in-place"),
				cancellationToken)
			.ConfigureAwait(false);

		_logger.LogInformation(
			"Erased customer {CustomerId} (request {RequestId}, status {Status})",
			customer.Id,
			result.RequestId,
			result.Status);

		return new CustomerErasureResponse(
			result.RequestId,
			result.Status,
			result.ScheduledExecutionTime,
			Pattern: "erase-in-place");
	}
}

/// <summary>
/// Handles <see cref="TombstoneCustomerCommand"/> by filing an audit-tracked
/// erasure request, replacing the customer with a tombstone marker, and
/// dispatching a <see cref="CustomerTombstonedEvent"/> for downstream projections.
/// </summary>
public sealed class TombstoneCustomerHandler : ICommandHandler<TombstoneCustomerCommand, CustomerErasureResponse>
{
	private readonly ICustomerRepository _repository;
	private readonly IErasureService _erasureService;
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<TombstoneCustomerHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TombstoneCustomerHandler"/> class.
	/// </summary>
	public TombstoneCustomerHandler(
		ICustomerRepository repository,
		IErasureService erasureService,
		IDispatcher dispatcher,
		ILogger<TombstoneCustomerHandler> logger)
	{
		_repository = repository;
		_erasureService = erasureService;
		_dispatcher = dispatcher;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<CustomerErasureResponse> HandleAsync(
		TombstoneCustomerCommand action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var customer = await _repository.FindAsync(action.CustomerId).ConfigureAwait(false)
			?? throw new KeyNotFoundException($"Customer {action.CustomerId} not found");

		var request = new ErasureRequest
		{
			DataSubjectId = customer.Id.ToString("D"),
			IdType = DataSubjectIdType.UserId,
			RequestedBy = action.RequestedBy,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Scope = ErasureScope.User,
		};
		var result = await _erasureService.RequestErasureAsync(request, cancellationToken).ConfigureAwait(false);

		await _repository.TombstoneAsync(action.CustomerId).ConfigureAwait(false);

		await _dispatcher
			.DispatchAsync(
				new CustomerTombstonedEvent(
					customer.Id,
					result.RequestId,
					result.Status,
					result.ScheduledExecutionTime),
				cancellationToken)
			.ConfigureAwait(false);

		_logger.LogInformation(
			"Tombstoned customer {CustomerId} (request {RequestId}, status {Status})",
			customer.Id,
			result.RequestId,
			result.Status);

		return new CustomerErasureResponse(
			result.RequestId,
			result.Status,
			result.ScheduledExecutionTime,
			Pattern: "tombstone");
	}
}
