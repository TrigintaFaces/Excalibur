// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#nullable enable

// Core Dispatch namespaces
global using Excalibur.Dispatch.Abstractions;
global using Excalibur.Dispatch.Abstractions.Delivery;
global using Excalibur.Dispatch.Messaging;
global using Excalibur.Dispatch.Routing;

// Shared test infrastructure
global using Tests.Shared;

// TestContainers
global using DotNet.Testcontainers.Containers;

// Testing and mocking
global using FakeItEasy;
global using Shouldly;
global using Xunit;

// Microsoft Extensions
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Type aliases for disambiguation
global using ContainerBuilder = DotNet.Testcontainers.Builders.ContainerBuilder;
global using FunctionalTestBase = Tests.Shared.FunctionalTestBase;
global using IAlbaHost = Alba.IAlbaHost;
global using IMessageBus = Excalibur.Dispatch.Abstractions.Transport.IMessageBus;
global using DispatchIMessageBus = Excalibur.Dispatch.Abstractions.Transport.IMessageBus;
global using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
global using PubSubContainer = Testcontainers.PubSub.PubSubContainer;
global using RoutingOptions = Excalibur.Dispatch.Options.Routing.RoutingOptions;
global using TestTimeouts = Tests.Shared.Infrastructure.TestTimeouts;

