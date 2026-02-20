// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;

namespace Tests.Shared.CQRS;

/// <summary>
/// Base class for test commands providing ICommand implementation.
/// </summary>
public abstract class TestCommandBase : ICommand
{
	/// <summary>
	/// Gets or sets the unique identifier for this command.
	/// </summary>
	public Guid CommandId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the timestamp when this command was created.
	/// </summary>
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;

	/// <inheritdoc/>
	public virtual ActivityType ActivityType => ActivityType.Command;

	/// <inheritdoc/>
	public virtual string ActivityName => GetType().Name;

	/// <inheritdoc/>
	public virtual string ActivityDisplayName => GetType().Name;

	/// <inheritdoc/>
	public virtual string ActivityDescription => $"Test command: {GetType().Name}";

	/// <inheritdoc/>
	public Guid CorrelationId { get; init; } = Guid.NewGuid();

	/// <inheritdoc/>
	public string TenantId { get; init; } = "test-tenant";
}

/// <summary>
/// Base class for test commands that return a result.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the command.</typeparam>
public abstract class TestCommandBase<TResult> : ICommand<TResult>
{
	/// <summary>
	/// Gets or sets the unique identifier for this command.
	/// </summary>
	public Guid CommandId { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the timestamp when this command was created.
	/// </summary>
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;

	/// <inheritdoc/>
	public virtual ActivityType ActivityType => ActivityType.Command;

	/// <inheritdoc/>
	public virtual string ActivityName => GetType().Name;

	/// <inheritdoc/>
	public virtual string ActivityDisplayName => GetType().Name;

	/// <inheritdoc/>
	public virtual string ActivityDescription => $"Test command: {GetType().Name}";

	/// <inheritdoc/>
	public Guid CorrelationId { get; init; } = Guid.NewGuid();

	/// <inheritdoc/>
	public string TenantId { get; init; } = "test-tenant";
}
