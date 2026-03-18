// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// The inbox background service (InboxService) is hosted inside the Excalibur.Outbox
// package and shares the same IProcessingGate singleton registered via
// outbox.WithLeaderElection(...). A single WithLeaderElection() call on the outbox
// builder gates both the OutboxBackgroundService and InboxService.
//
// No separate inbox.WithLeaderElection() extension is required.

namespace Excalibur.Outbox.DependencyInjection;
