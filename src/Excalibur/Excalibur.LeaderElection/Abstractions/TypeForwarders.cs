// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.LeaderElection;

// TypeForwarders for backward compatibility
// These types are defined in Excalibur.Dispatch.LeaderElection.Abstractions but re-exported here for Excalibur consumers
[assembly: TypeForwardedTo(typeof(ILeaderElection))]
[assembly: TypeForwardedTo(typeof(ILeaderElectionFactory))]
[assembly: TypeForwardedTo(typeof(IHealthBasedLeaderElection))]
[assembly: TypeForwardedTo(typeof(LeaderElectionOptions))]
[assembly: TypeForwardedTo(typeof(LeaderElectionEventArgs))]
[assembly: TypeForwardedTo(typeof(LeaderChangedEventArgs))]
[assembly: TypeForwardedTo(typeof(CandidateHealth))]
