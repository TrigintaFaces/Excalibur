// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// === Merged GlobalUsings from all 5 source Outbox test projects ===

// Test frameworks
global using Xunit;
global using Shouldly;
global using FakeItEasy;

// Shared test infrastructure
global using Tests.Shared;

// Microsoft extensions (from CosmosDb, DynamoDb, Firestore)
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

// Cloud-native abstractions (from DynamoDb, Firestore)
global using Excalibur.Data.Abstractions.CloudNative;

// Provider-specific namespaces (from CosmosDb, DynamoDb, Firestore)
global using Excalibur.Outbox.CosmosDb;
global using Excalibur.Outbox.DynamoDb;
global using Excalibur.Outbox.Firestore;

// SqlServer namespaces (needed because Excalibur.Outbox.Tests.SqlServer != Excalibur.Outbox.SqlServer)
global using Excalibur.Outbox.SqlServer;
global using Excalibur.Outbox.SqlServer.Requests;

// Null logger support (used by SqlServer tests)
global using Microsoft.Extensions.Logging.Abstractions;
