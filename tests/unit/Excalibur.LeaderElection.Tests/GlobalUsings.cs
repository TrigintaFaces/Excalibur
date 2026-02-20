// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

global using Excalibur.Dispatch.LeaderElection;

global using Excalibur.LeaderElection.Consul;
global using Excalibur.LeaderElection.InMemory;
global using Excalibur.LeaderElection.Kubernetes;
global using Excalibur.LeaderElection.Redis;
global using Excalibur.LeaderElection.SqlServer;

global using FakeItEasy;

global using k8s;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using Shouldly;

global using StackExchange.Redis;

global using Tests.Shared;

global using Xunit;
