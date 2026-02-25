// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

// Allow test projects to access internal types
[assembly: InternalsVisibleTo("Excalibur.Data.Tests.Unit")]
[assembly: InternalsVisibleTo("Excalibur.Data.Tests.Integration")]
[assembly: InternalsVisibleTo("Excalibur.Data.Tests.Functional")]
[assembly: InternalsVisibleTo("Excalibur.Tests.Unit")]
[assembly: InternalsVisibleTo("Excalibur.Tests")]
[assembly: InternalsVisibleTo("Excalibur.Data.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // For FakeItEasy
