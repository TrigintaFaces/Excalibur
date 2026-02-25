// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Conformance tests for InMemoryPersistenceProvider.
/// Verifies that the provider correctly implements the IPersistenceProvider contract.
/// </summary>
/// <remarks>
/// <para>
/// This test class demonstrates how to use the PersistenceProviderConformanceTestBase
/// to verify that a custom provider implementation correctly follows the interface contract.
/// </para>
/// <para>
/// To create conformance tests for your own provider:
/// <list type="number">
///   <item>Inherit from PersistenceProviderConformanceTestBase</item>
///   <item>Override CreateProvider() to create an instance of your provider</item>
///   <item>Override ExpectedProviderType with your provider's type string</item>
///   <item>Override ExpectedProviderName with your provider's name</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InMemoryPersistenceProviderConformanceShould : PersistenceProviderConformanceTestBase
{
	private readonly ILogger<InMemoryPersistenceProvider> _logger;

	public InMemoryPersistenceProviderConformanceShould()
	{
		_logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "InMemory";

	/// <inheritdoc/>
	protected override string ExpectedProviderName => "ConformanceTest";

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider()
	{
		var options = Options.Create(new InMemoryProviderOptions { Name = ExpectedProviderName });
		return new InMemoryPersistenceProvider(options, _logger);
	}
}
