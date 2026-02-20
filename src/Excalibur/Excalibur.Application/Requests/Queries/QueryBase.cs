// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Transactions;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Queries;

/// <summary>
/// Represents the base class for queries that return a value.
/// </summary>
/// <typeparam name="TResponse"> The type of response returned by the query. </typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="QueryBase{TResponse}" /> class with the specified correlation ID and tenant ID.
/// </remarks>
/// <param name="correlationId"> The correlation ID for the query. </param>
/// <param name="tenantId"> The tenant ID associated with the query. Defaults to TenantDefaults.DefaultTenantId if not provided. </param>
public abstract class QueryBase<TResponse>(Guid correlationId, string? tenantId = null) : IQuery<TResponse>
{
	private readonly Dictionary<string, object> _headers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryBase{TResponse}" /> class with default values.
	/// </summary>
	protected QueryBase() : this(Guid.Empty)
	{
	}

	/// <summary>
	/// Gets the unique identifier for this query as a GUID.
	/// </summary>
	/// <value> A unique identifier for this query instance. </value>
	public Guid Id { get; protected init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the unique identifier for this query as a string.
	/// </summary>
	/// <value> The string representation of the query's unique identifier. </value>
	public string MessageId => Id.ToString();

	/// <summary>
	/// Gets the type identifier for this query.
	/// </summary>
	/// <value> The fully qualified type name of the query. </value>
	public string MessageType => GetType().FullName ?? GetType().Name;

	/// <summary>
	/// Gets the kind of message this query represents.
	/// </summary>
	/// <value> Always returns <see cref="MessageKinds.Action" /> for queries. </value>
	public MessageKinds Kind { get; protected init; } = MessageKinds.Action;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> A read-only dictionary containing the query's metadata headers. </value>
	public IReadOnlyDictionary<string, object> Headers => new ReadOnlyDictionary<string, object>(_headers);

	/// <inheritdoc />
	ActivityType IActivity.ActivityType => ActivityType.Query;

	/// <inheritdoc />
	public string ActivityName => ActivityNameConvention.ResolveName(GetType());

	/// <inheritdoc />
	public virtual string ActivityDisplayName => ActivityNameConvention.ResolveDisplayName(GetType());

	/// <inheritdoc />
	public virtual string ActivityDescription => ActivityNameConvention.ResolveDescription(GetType());

	/// <inheritdoc />
	Guid IAmCorrelatable.CorrelationId => correlationId;

	/// <inheritdoc />
	public string TenantId => tenantId ?? TenantDefaults.DefaultTenantId;

	/// <inheritdoc />
	public TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <inheritdoc />
	public IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <inheritdoc />
	public TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(2);
}
