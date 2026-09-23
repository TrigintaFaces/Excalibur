// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Unified entry point combining <see cref="ISender"/> and <see cref="IPublisher"/>, providing the
/// request/response, streaming, and notification operations expected by code written against the
/// <c>IMediator</c> abstraction. Backed by the canonical Excalibur.Dispatch <c>IDispatcher</c>.
/// </summary>
public interface IMediator : ISender, IPublisher;
