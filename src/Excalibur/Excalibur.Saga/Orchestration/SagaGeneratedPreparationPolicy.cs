// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Saga.Orchestration;

/// <summary>Identifies the exclusively generated factory selection without a reflective fallback.</summary>
internal sealed class SagaGeneratedPreparationPolicy(SagaAtomicPreparationPolicy policy)
{
    internal SagaAtomicPreparationPolicy Policy { get; } = policy;
}
