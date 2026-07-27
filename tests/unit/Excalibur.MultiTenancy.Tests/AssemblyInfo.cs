// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

// Allow FakeItEasy/Castle DynamicProxy to fake interfaces closed over internal test types
// (e.g. IProjectionStore<TestProjection>).
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
