// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Dispatch namespaces
global using Excalibur.Dispatch.Abstractions;
global using Excalibur.Dispatch.Abstractions.Messaging;
global using Excalibur.Dispatch.Delivery;
global using Excalibur.Dispatch.Options.Delivery;

// Excalibur namespaces
global using Excalibur.Application;
global using Excalibur.Domain;
global using Excalibur.EventSourcing.Abstractions;

// Test infrastructure
global using Excalibur.Tests.Infrastructure;
global using Excalibur.Tests.Fixtures;

global using FakeItEasy;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using Shouldly;

global using Tests.Shared;

global using Xunit;

// Resolve IDispatchMessage ambiguity - use the Messaging version as primary
global using IDispatchMessage = Excalibur.Dispatch.Abstractions.IDispatchMessage;

using System.Runtime.CompilerServices;

// Assembly attributes for FakeItEasy dynamic proxy generation
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
