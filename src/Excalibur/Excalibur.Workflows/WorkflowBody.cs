// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// A durable workflow body: ordinary async logic whose only legal source of non-determinism is the
/// supplied <see cref="IWorkflowContext"/>. The engine drives the body under deterministic replay.
/// </summary>
/// <param name="context">The determinism boundary for this workflow instance.</param>
/// <param name="input">The workflow input supplied at start.</param>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
/// <returns>The workflow result, or <see langword="null"/> when the workflow produces none.</returns>
public delegate ValueTask<object?> WorkflowBody(
    IWorkflowContext context,
    object input,
    CancellationToken cancellationToken);
