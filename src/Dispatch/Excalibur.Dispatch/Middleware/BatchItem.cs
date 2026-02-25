// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents a single message within a batch.
/// </summary>
internal sealed record BatchItem(
	IDispatchMessage Message,
	IMessageContext Context,
	TaskCompletionSource<IMessageResult> CompletionSource,
	DispatchRequestDelegate NextDelegate);
