// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Multi-container fixture for end-to-end compliance testing with all providers.
/// </summary>
public class ComplianceMultiContainerFixture : IAsyncLifetime
{
	private readonly SqlServerContainerFixture _sqlServer = new();
	private readonly VaultContainerFixture _vault = new();
	private readonly LocalStackContainerFixture _localStack = new();

	/// <summary>
	/// Gets the SQL Server fixture.
	/// </summary>
	public SqlServerContainerFixture SqlServer => _sqlServer;

	/// <summary>
	/// Gets the Vault fixture.
	/// </summary>
	public VaultContainerFixture Vault => _vault;

	/// <summary>
	/// Gets the LocalStack fixture.
	/// </summary>
	public LocalStackContainerFixture LocalStack => _localStack;

	/// <summary>
	/// Gets a value indicating whether all containers are available.
	/// </summary>
	public bool AllContainersAvailable =>
		_sqlServer.DockerAvailable && _vault.DockerAvailable && _localStack.DockerAvailable;

	/// <summary>
	/// Gets a combined error message if any containers failed to start.
	/// </summary>
	public string? InitializationError
	{
		get
		{
			var errors = new List<string>();
			if (_sqlServer.InitializationError is not null)
				errors.Add($"SqlServer: {_sqlServer.InitializationError}");
			if (_vault.InitializationError is not null)
				errors.Add($"Vault: {_vault.InitializationError}");
			if (_localStack.InitializationError is not null)
				errors.Add($"LocalStack: {_localStack.InitializationError}");
			return errors.Count > 0 ? string.Join("; ", errors) : null;
		}
	}

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		// Start all containers in parallel for faster test startup
		await Task.WhenAll(
			_sqlServer.InitializeAsync(),
			_vault.InitializeAsync(),
			_localStack.InitializeAsync()
		).ConfigureAwait(true);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await Task.WhenAll(
			_sqlServer.DisposeAsync(),
			_vault.DisposeAsync(),
			_localStack.DisposeAsync()
		).ConfigureAwait(true);
	}
}

/// <summary>
/// Collection definition for multi-container end-to-end compliance tests.
/// </summary>
[CollectionDefinition(Name)]
public class ComplianceMultiContainerTestCollection : ICollectionFixture<ComplianceMultiContainerFixture>
{
	public const string Name = "ComplianceMultiContainer";
}
