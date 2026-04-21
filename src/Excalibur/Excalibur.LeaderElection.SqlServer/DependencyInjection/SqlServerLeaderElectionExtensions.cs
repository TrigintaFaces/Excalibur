// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// This file previously contained AddSqlServerLeaderElection(string, string) and
// AddSqlServerLeaderElectionFactory(string) overloads. These raw-parameter entry
// points have been deleted in favor of the builder pattern:
//
//   le.UseSqlServer(sql => sql.ConnectionString("...").LockResource("MyApp.Leader"))
//
// See SqlServerLeaderElectionBuilderExtensions.UseSqlServer for the replacement.
