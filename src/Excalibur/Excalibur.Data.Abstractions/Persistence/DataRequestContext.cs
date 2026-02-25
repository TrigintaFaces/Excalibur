// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Context for data request execution in the pipeline.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DataRequestContext" /> class.
/// </remarks>
/// <param name="request"> The data request. </param>
/// <param name="provider"> The persistence provider. </param>
/// <param name="requestType"> The type of the request. </param>
/// <param name="resultType"> The type of the result. </param>
public sealed class DataRequestContext(
	object request,
	IPersistenceProvider provider,
	Type requestType,
	Type resultType)
{
	/// <summary>
	/// Gets the data request.
	/// </summary>
	/// <value>The current <see cref="Request"/> value.</value>
	public object Request { get; } = request;

	/// <summary>
	/// Gets the persistence provider.
	/// </summary>
	/// <value>The current <see cref="Provider"/> value.</value>
	public IPersistenceProvider Provider { get; } = provider;

	/// <summary>
	/// Gets the type of the request.
	/// </summary>
	/// <value>The current <see cref="RequestType"/> value.</value>
	public Type RequestType { get; } = requestType;

	/// <summary>
	/// Gets the type of the result.
	/// </summary>
	/// <value>The current <see cref="ResultType"/> value.</value>
	public Type ResultType { get; } = resultType;

	/// <summary>
	/// Gets or sets the result of the request execution.
	/// </summary>
	/// <value>The current <see cref="Result"/> value.</value>
	public object? Result { get; set; }

	/// <summary>
	/// Gets or sets the exception if the execution failed.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Gets a dictionary for storing context items.
	/// </summary>
	/// <value>
	/// A dictionary for storing context items.
	/// </value>
	public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

	/// <summary>
	/// Gets a value indicating whether the execution was successful.
	/// </summary>
	/// <value>The current <see cref="IsSuccess"/> value.</value>
	public bool IsSuccess => Exception == null;

	/// <summary>
	/// Gets or sets the interception context.
	/// </summary>
	/// <value>The current <see cref="InterceptionContext"/> value.</value>
	public InterceptionContext? InterceptionContext { get; set; }
}
