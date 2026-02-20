// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Application.Requests.Queries;

/// <summary>
/// Represents a query in the system, combining CQRS and Dispatch patterns.
/// </summary>
/// <typeparam name="TResult"> The type of result returned by the query. </typeparam>
/// <remarks>
/// Queries are read-only operations that return data without modifying system state. This interface unifies the CQRS query pattern with
/// Dispatch's action pattern.
/// </remarks>
public interface IQuery<TResult> : IDispatchAction<TResult>, IActivity
{
}

/// <summary>
/// Defines a handler for queries in the CQRS pattern.
/// </summary>
/// <typeparam name="TQuery"> The type of query to handle. </typeparam>
/// <typeparam name="TResult"> The type of result returned by the query. </typeparam>
public interface IQueryHandler<in TQuery, TResult> : IActionHandler<TQuery, TResult>
	where TQuery : IQuery<TResult>
{
}
