// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents a single operation within a bulk optimization.
/// </summary>
internal sealed record BulkOperation(
	IDispatchMessage Message,
	IMessageContext Context,
	TaskCompletionSource<IMessageResult> CompletionSource);
