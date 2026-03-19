// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataOrchestrationManager"/>.
/// </summary>
[UnitTest]
public sealed class DataOrchestrationManagerShould : UnitTestBase
{
	private readonly Func<IDbConnection> _fakeConnectionFactory = () => A.Fake<IDbConnection>();
	private readonly IDataProcessorRegistry _fakeRegistry = A.Fake<IDataProcessorRegistry>();
	private readonly IServiceProvider _fakeServiceProvider = A.Fake<IServiceProvider>();
	private readonly IOptions<DataProcessingOptions> _fakeConfig =
		Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions());
	private readonly ILogger<DataOrchestrationManager> _fakeLogger = A.Fake<ILogger<DataOrchestrationManager>>();

	[Fact]
	public void CreateSuccessfully_WithValidDependencies()
	{
		// Act
		var manager = new DataOrchestrationManager(
			_fakeConnectionFactory, _fakeRegistry, _fakeServiceProvider, _fakeConfig, _fakeLogger);

		// Assert
		manager.ShouldNotBeNull();
		manager.ShouldBeAssignableTo<IDataOrchestrationManager>();
	}

	[Fact]
	public void Throw_WhenConnectionFactory_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(null!, _fakeRegistry, _fakeServiceProvider, _fakeConfig, _fakeLogger));
	}

	[Fact]
	public void Throw_WhenProcessorRegistry_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(_fakeConnectionFactory, null!, _fakeServiceProvider, _fakeConfig, _fakeLogger));
	}

	[Fact]
	public void Throw_WhenServiceProvider_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(_fakeConnectionFactory, _fakeRegistry, null!, _fakeConfig, _fakeLogger));
	}

	[Fact]
	public void Throw_WhenConfiguration_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(_fakeConnectionFactory, _fakeRegistry, _fakeServiceProvider, null!, _fakeLogger));
	}

	[Fact]
	public void Throw_WhenLogger_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(_fakeConnectionFactory, _fakeRegistry, _fakeServiceProvider, _fakeConfig, null!));
	}
}
