// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Inspection;

/// <summary>
/// In-memory implementation of <see cref="ISagaStateInspector"/> that reads from
/// the registered <see cref="Abstractions.ISagaStateStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation delegates to the existing <see cref="Abstractions.ISagaStateStore"/> for
/// state retrieval. It provides a read-only view suitable for monitoring and
/// diagnostics without exposing the full store API.
/// </para>
/// </remarks>
public sealed class InMemorySagaStateInspector : ISagaStateInspector
{
	private readonly Abstractions.ISagaStateStore _stateStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemorySagaStateInspector"/> class.
	/// </summary>
	/// <param name="stateStore">The saga state store to read from.</param>
	public InMemorySagaStateInspector(Abstractions.ISagaStateStore stateStore)
	{
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
	}

	/// <inheritdoc />
	public async Task<SagaState?> GetStateAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		return await _stateStore.GetStateAsync(sagaId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<StepExecutionRecord>> GetHistoryAsync(
		string sagaId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		var state = await _stateStore.GetStateAsync(sagaId, cancellationToken).ConfigureAwait(false);

		if (state is null)
		{
			return [];
		}

		return state.StepHistory.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public async Task<string?> GetActiveStepAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		var state = await _stateStore.GetStateAsync(sagaId, cancellationToken).ConfigureAwait(false);

		if (state is null || state.Status != SagaStatus.Running)
		{
			return null;
		}

		// Return the last step that hasn't completed
		var activeStep = state.StepHistory
			.LastOrDefault(s => s.CompletedAt is null);

		return activeStep?.StepName;
	}
}
