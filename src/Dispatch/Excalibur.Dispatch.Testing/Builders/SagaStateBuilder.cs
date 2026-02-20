// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Testing.Builders;

/// <summary>
/// Fluent builder for creating <see cref="SagaState"/> instances in tests.
/// Provides sensible defaults with override capability for saga state properties.
/// </summary>
/// <typeparam name="TSagaState">The saga state type to build. Must inherit from <see cref="SagaState"/>
/// and have a parameterless constructor.</typeparam>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// var state = new SagaStateBuilder&lt;OrderSagaState&gt;()
///     .WithSagaId(Guid.NewGuid())
///     .WithCompleted(false)
///     .Configure(s => s.OrderId = "order-123")
///     .Build();
/// </code>
/// </para>
/// </remarks>
public sealed class SagaStateBuilder<TSagaState>
	where TSagaState : SagaState, new()
{
	private Guid? _sagaId;
	private bool _completed;
	private readonly List<Action<TSagaState>> _configurators = [];

	/// <summary>
	/// Sets the saga ID. If not set, a new GUID is generated.
	/// </summary>
	/// <param name="sagaId">The saga ID.</param>
	/// <returns>This builder for chaining.</returns>
	public SagaStateBuilder<TSagaState> WithSagaId(Guid sagaId)
	{
		_sagaId = sagaId;
		return this;
	}

	/// <summary>
	/// Sets the completed flag.
	/// </summary>
	/// <param name="completed">Whether the saga is completed.</param>
	/// <returns>This builder for chaining.</returns>
	public SagaStateBuilder<TSagaState> WithCompleted(bool completed)
	{
		_completed = completed;
		return this;
	}

	/// <summary>
	/// Configures additional properties on the saga state via a callback.
	/// Multiple calls are accumulated and applied in order.
	/// </summary>
	/// <param name="configure">Action to configure the saga state.</param>
	/// <returns>This builder for chaining.</returns>
	public SagaStateBuilder<TSagaState> Configure(Action<TSagaState> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_configurators.Add(configure);
		return this;
	}

	/// <summary>
	/// Builds the saga state with the configured properties.
	/// </summary>
	/// <returns>A new saga state instance.</returns>
	public TSagaState Build()
	{
		var state = new TSagaState
		{
			SagaId = _sagaId ?? Guid.NewGuid(),
			Completed = _completed
		};

		foreach (var configure in _configurators)
		{
			configure(state);
		}

		return state;
	}

	/// <summary>
	/// Builds multiple saga state instances with unique IDs.
	/// Each instance gets a unique SagaId. Configure callbacks apply to all instances.
	/// </summary>
	/// <param name="count">Number of saga states to build.</param>
	/// <returns>A list of saga state instances.</returns>
	public List<TSagaState> BuildMany(int count)
	{
		return [.. Enumerable.Range(0, count).Select(_ =>
		{
			var state = new TSagaState
			{
				SagaId = Guid.NewGuid(),
				Completed = _completed
			};

			foreach (var configure in _configurators)
			{
				configure(state);
			}

			return state;
		})];
	}
}
