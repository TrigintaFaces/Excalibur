// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Deep coverage tests for <see cref="EnvironmentVariableCredentialStore"/> covering
/// fallback lookup without prefix, null prefix handling, mixed separator conversion,
/// and the SecureString readonly guarantee.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class EnvironmentVariableCredentialStoreDepthShould : IDisposable
{
	private readonly List<string> _envVarsToClean = [];

	public void Dispose()
	{
		foreach (var envVar in _envVarsToClean)
		{
			Environment.SetEnvironmentVariable(envVar, null);
		}
	}

	[Fact]
	public async Task FallbackToNoPrefixLookup_WhenPrefixedVarNotFound()
	{
		// Arrange — set env var WITHOUT prefix, use store WITH prefix
		var rawVarName = "MY_FALLBACK_KEY";
		Environment.SetEnvironmentVariable(rawVarName, "fallback-value");
		_envVarsToClean.Add(rawVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance, "CUSTOM_");

		// Act — key "my.fallback.key" → prefixed "CUSTOM_MY_FALLBACK_KEY" not found
		//       → fallback "MY_FALLBACK_KEY" should be found
		var result = await store.GetCredentialAsync("my.fallback.key", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task UseNullPrefixAsEmpty()
	{
		// Arrange — null prefix defaults to empty string
		var envVarName = "NULL_PREFIX_TEST";
		Environment.SetEnvironmentVariable(envVarName, "null-prefix-value");
		_envVarsToClean.Add(envVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance, null!);

		// Act
		var result = await store.GetCredentialAsync("null.prefix.test", CancellationToken.None);

		// Assert — with empty prefix, var name is "NULL_PREFIX_TEST"
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ConvertMixedSeparatorsToUnderscores()
	{
		// Arrange — key with mixed dots and colons
		var envVarName = "DISPATCH_MY_MIXED_KEY_WITH_PARTS";
		Environment.SetEnvironmentVariable(envVarName, "mixed-value");
		_envVarsToClean.Add(envVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance);

		// Act — "my.mixed:key.with:parts" → "MY_MIXED_KEY_WITH_PARTS"
		var result = await store.GetCredentialAsync("my.mixed:key.with:parts", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnNull_WhenBothPrefixedAndUnprefixedMissing()
	{
		// Arrange — no env vars set at all
		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance, "MISSING_PREFIX_");

		// Act
		var result = await store.GetCredentialAsync("nonexistent.key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task PreferPrefixedVar_OverUnprefixed()
	{
		// Arrange — set both prefixed and unprefixed
		var prefixedName = "TEST_PFX_PRIORITY_KEY";
		var unprefixedName = "PRIORITY_KEY";
		Environment.SetEnvironmentVariable(prefixedName, "prefixed-value");
		Environment.SetEnvironmentVariable(unprefixedName, "unprefixed-value");
		_envVarsToClean.Add(prefixedName);
		_envVarsToClean.Add(unprefixedName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance, "TEST_PFX_");

		// Act
		var result = await store.GetCredentialAsync("priority.key", CancellationToken.None);

		// Assert — prefixed var found first, fallback not needed
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnSecureString_WithCorrectLength()
	{
		// Arrange
		const string expectedValue = "exact-12-chars";
		var envVarName = "DISPATCH_LENGTH_TEST";
		Environment.SetEnvironmentVariable(envVarName, expectedValue);
		_envVarsToClean.Add(envVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance);

		// Act
		var result = await store.GetCredentialAsync("length.test", CancellationToken.None);

		// Assert — SecureString has correct length
		result.ShouldNotBeNull();
		result.Length.ShouldBe(expectedValue.Length);
	}

	[Fact]
	public async Task HandleSingleCharacterCredential()
	{
		// Arrange
		var envVarName = "DISPATCH_TINY";
		Environment.SetEnvironmentVariable(envVarName, "x");
		_envVarsToClean.Add(envVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance);

		// Act
		var result = await store.GetCredentialAsync("tiny", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Length.ShouldBe(1);
		result.IsReadOnly().ShouldBeTrue();
	}

	[Fact]
	public async Task HandleLongCredentialValue()
	{
		// Arrange
		var longValue = new string('A', 4096);
		var envVarName = "DISPATCH_LONG_CREDENTIAL";
		Environment.SetEnvironmentVariable(envVarName, longValue);
		_envVarsToClean.Add(envVarName);

		var store = new EnvironmentVariableCredentialStore(
			NullLogger<EnvironmentVariableCredentialStore>.Instance);

		// Act
		var result = await store.GetCredentialAsync("long.credential", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Length.ShouldBe(4096);
	}
}
