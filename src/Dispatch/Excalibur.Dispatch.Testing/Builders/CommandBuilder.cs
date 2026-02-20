// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Builders;

/// <summary>
/// Fluent builder for creating command messages in tests.
/// Wraps a command with context metadata for testing dispatch scenarios.
/// </summary>
/// <typeparam name="TCommand">The command type to build.</typeparam>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// var (command, context) = new CommandBuilder&lt;CreateOrderCommand&gt;()
///     .WithCommand(new CreateOrderCommand { OrderId = "123" })
///     .WithCorrelationId("corr-456")
///     .WithTenantId("tenant-abc")
///     .Build();
/// </code>
/// </para>
/// </remarks>
public sealed class CommandBuilder<TCommand>
	where TCommand : IDispatchAction, new()
{
	private TCommand? _command;
	private readonly MessageContextBuilder _contextBuilder = new();

	/// <summary>
	/// Sets the command instance. If not set, a new instance is created via default constructor.
	/// </summary>
	/// <param name="command">The command instance.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithCommand(TCommand command)
	{
		_command = command;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID on the message context.
	/// </summary>
	/// <param name="correlationId">The correlation ID.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithCorrelationId(string correlationId)
	{
		_contextBuilder.WithCorrelationId(correlationId);
		return this;
	}

	/// <summary>
	/// Sets the causation ID on the message context.
	/// </summary>
	/// <param name="causationId">The causation ID.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithCausationId(string causationId)
	{
		_contextBuilder.WithCausationId(causationId);
		return this;
	}

	/// <summary>
	/// Sets the tenant ID on the message context.
	/// </summary>
	/// <param name="tenantId">The tenant ID.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithTenantId(string tenantId)
	{
		_contextBuilder.WithTenantId(tenantId);
		return this;
	}

	/// <summary>
	/// Sets the user ID on the message context.
	/// </summary>
	/// <param name="userId">The user ID.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithUserId(string userId)
	{
		_contextBuilder.WithUserId(userId);
		return this;
	}

	/// <summary>
	/// Sets the service provider on the message context.
	/// </summary>
	/// <param name="services">The service provider.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithRequestServices(IServiceProvider services)
	{
		_contextBuilder.WithRequestServices(services);
		return this;
	}

	/// <summary>
	/// Adds a custom item to the message context.
	/// </summary>
	/// <param name="key">The item key.</param>
	/// <param name="value">The item value.</param>
	/// <returns>This builder for chaining.</returns>
	public CommandBuilder<TCommand> WithContextItem(string key, object value)
	{
		_contextBuilder.WithItem(key, value);
		return this;
	}

	/// <summary>
	/// Builds the command and its associated message context.
	/// </summary>
	/// <returns>A tuple of the command and its message context.</returns>
	public (TCommand Command, IMessageContext Context) Build()
	{
		var command = _command ?? new TCommand();
		var context = _contextBuilder
			.WithMessage(command)
			.Build();

		return (command, context);
	}

	/// <summary>
	/// Builds only the command instance without a context.
	/// </summary>
	/// <returns>The command instance.</returns>
	public TCommand BuildCommand()
	{
		return _command ?? new TCommand();
	}
}
