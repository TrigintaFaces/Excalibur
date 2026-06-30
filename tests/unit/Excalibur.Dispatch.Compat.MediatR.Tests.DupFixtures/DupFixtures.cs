// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compat.MediatR;

namespace Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures;

/// <summary>Marker to resolve this fixture assembly for the dup-handler lock (nvzo4h).</summary>
public static class DupFixtureMarker;

/// <summary>A request type that (illegally) has two registered handlers in this assembly.</summary>
public sealed class DupRequest : IRequest<string>;

/// <summary>First handler for <see cref="DupRequest"/>.</summary>
public sealed class DupHandlerOne : IRequestHandler<DupRequest, string>
{
    public Task<string> Handle(DupRequest request, CancellationToken cancellationToken) => Task.FromResult("one");
}

/// <summary>Second handler for <see cref="DupRequest"/> — the duplicate (AC-8 fail-fast).</summary>
public sealed class DupHandlerTwo : IRequestHandler<DupRequest, string>
{
    public Task<string> Handle(DupRequest request, CancellationToken cancellationToken) => Task.FromResult("two");
}
