// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Marker interface for a streaming request that yields an asynchronous sequence of
/// <typeparamref name="TResponse"/> items. Provides the <c>IStreamRequest&lt;TResponse&gt;</c> shape
/// used by MediatR-based code; maps to the canonical <c>IStreamingDispatcher</c>.
/// </summary>
/// <typeparam name="TResponse">The element type of the streamed response.</typeparam>
public interface IStreamRequest<out TResponse>;
