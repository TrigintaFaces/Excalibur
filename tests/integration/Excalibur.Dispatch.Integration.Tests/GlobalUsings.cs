// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

global using System.Diagnostics;
global using System.Diagnostics.Metrics;

global using Excalibur.Dispatch.Abstractions;
global using Excalibur.Dispatch.Abstractions.Delivery;

global using FakeItEasy;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using Shouldly;

global using Tests.Shared;
global using Tests.Shared.Categories;
global using Tests.Shared.Fixtures;

global using Xunit;

// Test type aliases
global using TestMessage = Tests.Shared.TestTypes.TestMessage;
global using TestEvent = Tests.Shared.TestTypes.TestEvent;
global using TestsShared = Tests.Shared;

// Testcontainers
global using PostgreSqlContainer = Testcontainers.PostgreSql.PostgreSqlContainer;
global using MsSqlContainer = Testcontainers.MsSql.MsSqlContainer;
global using RabbitMqContainer = Testcontainers.RabbitMq.RabbitMqContainer;

// Core types
global using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

// Test base classes
global using IntegrationTestBase = Tests.Shared.IntegrationTestBase;
global using TestContainersTestBase = Excalibur.Dispatch.Integration.Tests.DispatchCore.Infrastructure.TestContainersTestBase;

// Database fixtures
global using SqlServerFixture = Tests.Shared.Fixtures.SqlServerContainerFixture;
global using PostgresFixture = Tests.Shared.Fixtures.PostgresContainerFixture;
