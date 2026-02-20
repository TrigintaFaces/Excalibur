// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Queries;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Represents a query that requires authorization.
/// </summary>
/// <typeparam name="TResponse"> The type of response produced by the query. </typeparam>
/// <remarks>
/// Combines the functionalities of <see cref="IQuery{TResponse}" /> and <see cref="IRequireAuthorization" />, enabling access control for
/// queries within the system.
/// </remarks>
public interface IAuthorizeQuery<TResponse> : IQuery<TResponse>, IRequireAuthorization;
