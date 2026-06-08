// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Tests.DependencyInjection;

// Stand-in for A3.Audit's internal fail-fast fallback dispatcher, declared in the SAME namespace
// so its Type.FullName matches the string the outbox registration uses to remove the stub
// (Excalibur.A3.Audit.Internal.DefaultOutboxDispatcher). Excalibur.Outbox.Tests does not reference
// Excalibur.A3, so this faithfully exercises the by-name removal without taking that dependency.
namespace Excalibur.A3.Audit.Internal;

internal sealed class DefaultOutboxDispatcher : TestOutboxDispatcherBase;
