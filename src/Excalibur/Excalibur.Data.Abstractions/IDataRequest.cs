// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Non-generic base interface for data requests.
/// </summary>
public interface IDataRequest
{
	/// <summary>
	/// Gets the unique request identifier.
	/// </summary>
	/// <value>
	/// The unique request identifier.
	/// </value>
	string RequestId { get; }

	/// <summary>
	/// Gets the request type name.
	/// </summary>
	/// <value>
	/// The request type name.
	/// </value>
	string RequestType { get; }

	/// <summary>
	/// Gets the timestamp when the request was created.
	/// </summary>
	/// <value>
	/// The timestamp when the request was created.
	/// </value>
	DateTimeOffset CreatedAt { get; }

	/// <summary>
	/// Gets the optional correlation identifier for tracking related requests.
	/// </summary>
	/// <value>
	/// The optional correlation identifier for tracking related requests.
	/// </value>
	string? CorrelationId { get; }

	/// <summary>
	/// Gets additional metadata associated with the request.
	/// </summary>
	/// <value>
	/// Additional metadata associated with the request.
	/// </value>
	IDictionary<string, object>? Metadata { get; }
}

/// <summary>
/// Represents a contract for database requests that define the command and parameters necessary for execution.
/// </summary>
/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
/// <typeparam name="TModel"> The type of the model to be returned by the request. </typeparam>
public interface IDataRequest<in TConnection, TModel> : IDataRequest
{
	/// <summary>
	/// Gets the command definition used for executing the request.
	/// </summary>
	/// <value>
	/// The command definition used for executing the request.
	/// </value>
	CommandDefinition Command { get; }

	/// <summary>
	/// Gets the parameters associated with the request.
	/// </summary>
	/// <value>
	/// The parameters associated with the request.
	/// </value>
	DynamicParameters Parameters { get; }

	/// <summary>
	/// Gets the function responsible for resolving the request result using the provided connection.
	/// </summary>
	/// <value>
	/// The function responsible for resolving the request result using the provided connection.
	/// </value>
	Func<TConnection, Task<TModel>> ResolveAsync { get; }
}
