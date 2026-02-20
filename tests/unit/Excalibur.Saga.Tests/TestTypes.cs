// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests;

/// <summary>
/// Shared test data type for saga step tests.
/// Must be public for FakeItEasy to create proxies.
/// </summary>
public sealed class TestSagaData
{
	public string? Value { get; set; }
	public int Counter { get; set; }
}
