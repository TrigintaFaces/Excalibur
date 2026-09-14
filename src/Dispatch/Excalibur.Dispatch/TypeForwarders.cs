// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;

// These types moved to another assembly. SOURCE compatibility survives on its own, because this package
// references the one they moved to. BINARY compatibility does not: an assembly compiled against an earlier
// version records the type as living HERE and raises a TypeLoadException against the new layout -- which no
// build and no test detects, because everything we compile is rebuilt from source.
//
// A forwarder keeps the type reachable through this assembly, so it remains part of THIS package's public
// API and is declared in this package's baseline with the "(forwarded, contained in ...)" suffix. Removing
// a line here re-breaks a consumer holding a compiled reference.
[assembly: TypeForwardedTo(typeof(CircuitState))]
[assembly: TypeForwardedTo(typeof(DeadLetterFilter))]
[assembly: TypeForwardedTo(typeof(DeadLetterMessage))]
[assembly: TypeForwardedTo(typeof(IDeadLetterStore))]
[assembly: TypeForwardedTo(typeof(IDeadLetterStoreAdmin))]
[assembly: TypeForwardedTo(typeof(MessageContext))]
[assembly: TypeForwardedTo(typeof(WellKnownHeaderNames))]
