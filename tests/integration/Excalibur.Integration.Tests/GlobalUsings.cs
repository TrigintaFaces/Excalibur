// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

global using Excalibur.Dispatch.Abstractions;
global using Excalibur.Dispatch.Abstractions.Delivery;
global using Excalibur.Dispatch.Abstractions.Serialization;
global using Excalibur.Dispatch.Messaging;
global using FakeItEasy;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Shouldly;
global using Tests.Shared;
global using Tests.Shared.Fixtures;
global using Tests.Shared.TestTypes;
global using Xunit;
