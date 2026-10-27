// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;

namespace Excalibur.Dispatch.Scheduling.Aot.Sample;

public sealed record ScheduledAction(string Payload, string TenantId, Guid? CorrelationId) : IDispatchAction, ITenantAware, ICorrelationAware
{
	public Guid? CorrelationId { get; set; } = CorrelationId;
}

public sealed record ScheduledDocument(string Payload, string TenantId, Guid? CorrelationId) : IDispatchDocument, ITenantAware, ICorrelationAware
{
	public Guid? CorrelationId { get; set; } = CorrelationId;
}

public sealed record UndeclaredAction(string Payload) : IDispatchAction;

public sealed record DeliveryObservation(string Payload, string? Tenant, string? AmbientTenant, string? Correlation);

public sealed class VerificationState
{
	private int _actionInvocations;
	private int _documentInvocations;
	private Exception? _handlerFault;

	public TaskCompletionSource<DeliveryObservation> Action { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public TaskCompletionSource<DeliveryObservation> Document { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public void RecordActionInvocation() => AssertFirstInvocation(Interlocked.Increment(ref _actionInvocations));
	public void RecordDocumentInvocation() => AssertFirstInvocation(Interlocked.Increment(ref _documentInvocations));
	public void RecordHandlerFault(Exception exception) => Interlocked.CompareExchange(ref _handlerFault, exception, null);
	public void AssertFinalCounts()
	{
		if (Volatile.Read(ref _actionInvocations) != 1 || Volatile.Read(ref _documentInvocations) != 1 || Volatile.Read(ref _handlerFault) is not null)
		{
			throw new InvalidOperationException("Scheduled delivery must invoke each handler exactly once without a caught handler fault.", _handlerFault);
		}
	}
	private void AssertFirstInvocation(int count)
	{
		if (count != 1)
		{
			var fault = new InvalidOperationException("A one-time scheduled handler was invoked more than once.");
			RecordHandlerFault(fault);
			throw fault;
		}
	}
	public static void AssertDelivery(DeliveryObservation actual, string payload, string tenant, Guid correlation)
	{
		if (actual.Payload != payload || actual.Tenant != tenant || actual.AmbientTenant != tenant || actual.Correlation != correlation.ToString())
		{
			throw new InvalidOperationException("The scheduled handler received an incorrect payload or execution identity.");
		}
	}
}

public sealed class ScheduledActionHandler(VerificationState state) : IActionHandler<ScheduledAction>, IMessageContextAware
{
	private IMessageContext? _context;
	public void SetContext(IMessageContext context) => _context = context;
	public Task HandleAsync(ScheduledAction action, CancellationToken cancellationToken)
	{
		state.RecordActionInvocation();
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var context = _context ?? throw new InvalidOperationException("Action handler context was not populated.");
			if (!state.Action.TrySetResult(new DeliveryObservation(action.Payload, context.GetTenantId(), TenantContextHolder.Current, context.CorrelationId)))
			{
				throw new InvalidOperationException("The one-time action was delivered more than once.");
			}
			return Task.CompletedTask;
		}
		catch (Exception exception)
		{
			state.RecordHandlerFault(exception);
			throw;
		}
	}
}

public sealed class ScheduledDocumentHandler(VerificationState state) : IDocumentHandler<ScheduledDocument>, IMessageContextAware
{
	private IMessageContext? _context;
	public void SetContext(IMessageContext context) => _context = context;
	public Task HandleAsync(ScheduledDocument document, CancellationToken cancellationToken)
	{
		state.RecordDocumentInvocation();
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var context = _context ?? throw new InvalidOperationException("Document handler context was not populated.");
			if (!state.Document.TrySetResult(new DeliveryObservation(document.Payload, context.GetTenantId(), TenantContextHolder.Current, context.CorrelationId)))
			{
				throw new InvalidOperationException("The one-time document was delivered more than once.");
			}
			return Task.CompletedTask;
		}
		catch (Exception exception)
		{
			state.RecordHandlerFault(exception);
			throw;
		}
	}
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ScheduledAction))]
[JsonSerializable(typeof(ScheduledDocument))]
internal sealed partial class SchedulingJsonContext : JsonSerializerContext;
