// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Unified session manager interface that combines all session management capabilities.
/// </summary>
/// <remarks>
/// This interface consolidates lock coordination, session information, lifecycle management, state management, and advanced locking to
/// provide a complete session management solution.
/// </remarks>
public interface ISessionManager :
	ISessionLockCoordinator,
	ISessionInfoProvider,
	ISessionLifecycleManager,
	ISessionStateManager,
	IAdvancedSessionLocking;
