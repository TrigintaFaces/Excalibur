// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Runtime.CompilerServices;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

// These types moved to another assembly. SOURCE compatibility survives on its own, because this package
// references the one they moved to. BINARY compatibility does not: an assembly compiled against an earlier
// version records the type as living HERE and raises a TypeLoadException against the new layout -- which no
// build and no test detects, because everything we compile is rebuilt from source.
//
// A forwarder keeps the type reachable through this assembly, so it remains part of THIS package's public
// API and is declared in this package's baseline with the "(forwarded, contained in ...)" suffix. Removing
// a line here re-breaks a consumer holding a compiled reference.
[assembly: TypeForwardedTo(typeof(AuthenticationState))]
[assembly: TypeForwardedTo(typeof(IAuthenticationToken))]
[assembly: TypeForwardedTo(typeof(AuthorizationPolicyExtensions))]
[assembly: TypeForwardedTo(typeof(IAuthorizationPolicy))]
[assembly: TypeForwardedTo(typeof(IAuthorizationPolicyProvider))]
[assembly: TypeForwardedTo(typeof(IPolicy))]
[assembly: TypeForwardedTo(typeof(IPolicyProvider<>))]
[assembly: TypeForwardedTo(typeof(AuthorizationPolicy))]
[assembly: TypeForwardedTo(typeof(PolicyResult))]
