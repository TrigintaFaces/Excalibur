// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides functionality to retrieve an authorization policy for the current user.
/// </summary>
public interface IAuthorizationPolicyProvider : IPolicyProvider<IAuthorizationPolicy>;
