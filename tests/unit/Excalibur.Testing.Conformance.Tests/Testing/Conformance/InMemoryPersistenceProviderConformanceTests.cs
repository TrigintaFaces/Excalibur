// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;
using Excalibur.Data.Persistence;
using Excalibur.Testing.Conformance;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="PersistenceProviderConformanceTestKit"/> runs end-to-end against a sample
/// <see cref="IPersistenceProvider"/> implementation (<see cref="InMemoryPersistenceProvider"/>) and reports
/// pass/fail (wired-and-tested).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class InMemoryPersistenceProviderConformanceTests : PersistenceProviderConformanceTestKit
{
	private readonly ILogger<InMemoryPersistenceProvider> _logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();

	/// <inheritdoc />
	protected override string ExpectedProviderType => "InMemory";

	/// <inheritdoc />
	protected override string ExpectedProviderName => "ConformanceTest";

	/// <inheritdoc />
	protected override IPersistenceProvider CreateProvider()
	{
		var options = Options.Create(new InMemoryProviderOptions { Name = ExpectedProviderName });
		return new InMemoryPersistenceProvider(options, _logger);
	}

	[Fact] public void Provider_ShouldHaveNonNullName_Test() => Provider_ShouldHaveNonNullName();
	[Fact] public void Provider_ShouldHaveExpectedName_Test() => Provider_ShouldHaveExpectedName();
	[Fact] public void Provider_ShouldHaveNonNullProviderType_Test() => Provider_ShouldHaveNonNullProviderType();
	[Fact] public void Provider_ShouldHaveExpectedProviderType_Test() => Provider_ShouldHaveExpectedProviderType();
	[Fact] public void Provider_ShouldHaveNonNullConnectionString_Test() => Provider_ShouldHaveNonNullConnectionString();
	[Fact] public void Provider_ShouldHaveNonNullRetryPolicy_Test() => Provider_ShouldHaveNonNullRetryPolicy();
	[Fact] public void CreateTransactionScope_ShouldReturnNonNullScope_Test() => CreateTransactionScope_ShouldReturnNonNullScope();
	[Fact] public void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope_Test() => CreateTransactionScope_WithIsolationLevel_ShouldReturnScope();
	[Fact] public void CreateTransactionScope_WithTimeout_ShouldReturnScope_Test() => CreateTransactionScope_WithTimeout_ShouldReturnScope();
	[Fact] public Task GetMetricsAsync_ShouldReturnNonNullDictionary_Test() => GetMetricsAsync_ShouldReturnNonNullDictionary();
	[Fact] public Task GetMetricsAsync_ShouldContainProviderKey_Test() => GetMetricsAsync_ShouldContainProviderKey();
	[Fact] public void Dispose_ShouldNotThrow_Test() => Dispose_ShouldNotThrow();
	[Fact] public void Dispose_CalledMultipleTimes_ShouldNotThrow_Test() => Dispose_CalledMultipleTimes_ShouldNotThrow();
	[Fact] public Task DisposeAsync_ShouldNotThrow_Test() => DisposeAsync_ShouldNotThrow();
	[Fact] public void IsAvailable_AfterDispose_ShouldBeFalse_Test() => IsAvailable_AfterDispose_ShouldBeFalse();
	[Fact] public void Provider_ShouldImplementIDisposable_Test() => Provider_ShouldImplementIDisposable();
	[Fact] public void Provider_ShouldImplementIAsyncDisposable_Test() => Provider_ShouldImplementIAsyncDisposable();
}
