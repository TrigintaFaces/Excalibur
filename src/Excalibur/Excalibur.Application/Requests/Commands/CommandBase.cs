// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Transactions;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Commands;

/// <summary>
/// Represents the base class for commands that do not return a value.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CommandBase" /> class with a specified correlation ID and tenant ID. </remarks>
/// <param name="correlationId"> The correlation ID associated with the command. </param>
/// <param name="tenantId"> The tenant ID associated with the command. Defaults to null. </param>
public abstract class CommandBase(Guid correlationId, string? tenantId = null) : ICommand
{
	private readonly Dictionary<string, object> _headers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandBase" /> class with default values.
	/// </summary>
	protected CommandBase() : this(Guid.Empty) { }

	/// <summary>
	/// Gets the unique identifier for this command as a GUID.
	/// </summary>
	/// <value> A unique identifier for this command instance. </value>
	public Guid Id { get; protected init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the unique identifier for this command as a string.
	/// </summary>
	/// <value> The string representation of the command's unique identifier. </value>
	public string MessageId => Id.ToString();

	/// <summary>
	/// Gets the type identifier for this command.
	/// </summary>
	/// <value> The fully qualified type name of the command. </value>
	public string MessageType => GetType().FullName ?? GetType().Name;

	/// <summary>
	/// Gets the kind of message this command represents.
	/// </summary>
	/// <value> Always returns <see cref="MessageKinds.Action" /> for commands. </value>
	public MessageKinds Kind { get; protected init; } = MessageKinds.Action;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> A read-only dictionary containing the command's metadata headers. </value>
	public IReadOnlyDictionary<string, object> Headers => new ReadOnlyDictionary<string, object>(_headers);

	/// <inheritdoc />
	ActivityType IActivity.ActivityType => ActivityType.Command;

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
	public virtual TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <inheritdoc />
	public virtual IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <inheritdoc />
	public virtual TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Represents the base class for commands with a specific response type.
/// </summary>
/// <typeparam name="TResponse"> The type of the response returned by the command. </typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="CommandBase{TResponse}" /> class with a specified correlation ID and tenant ID.
/// </remarks>
/// <param name="correlationId"> The correlation ID associated with the command. </param>
/// <param name="tenantId"> The tenant ID associated with the command. Defaults to <see cref="TenantDefaults.DefaultTenantId"/> if null. </param>
// R0.8: File name should match first type name
#pragma warning disable SA1649
// R0.8: File may only contain a single type
#pragma warning disable SA1402

public abstract class CommandBase<TResponse>(Guid correlationId, string? tenantId = null)
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1649 // File name should match first type name
	: CommandBase(correlationId, tenantId), ICommand<TResponse>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CommandBase{TResponse}" /> class.
	/// </summary>
	protected CommandBase()
		: this(Guid.Empty)
	{
	}
}
