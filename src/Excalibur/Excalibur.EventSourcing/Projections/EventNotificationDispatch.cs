// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Closes notification dispatch over event types already known at projection registration.</summary>
internal interface IEventNotificationDispatch
{
	Type EventType { get; }
	IEnumerable<object> GetHandlers(IServiceProvider services);
	Task InvokeAsync(object handler, IDomainEvent domainEvent, EventNotificationContext context, CancellationToken token);
}

internal sealed class EventNotificationDispatch<TEvent> : IEventNotificationDispatch where TEvent : IDomainEvent
{
	public Type EventType => typeof(TEvent);
	public IEnumerable<object> GetHandlers(IServiceProvider services) => services.GetServices<IEventNotificationHandler<TEvent>>();
	public Task InvokeAsync(object handler, IDomainEvent domainEvent, EventNotificationContext context, CancellationToken token) =>
		((IEventNotificationHandler<TEvent>)handler).HandleAsync((TEvent)domainEvent, context, token);
}
