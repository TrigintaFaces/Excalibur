// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IEncryptionProviderRegistry"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit validates that implementations of <see cref="IEncryptionProviderRegistry"/>
/// correctly implement multi-provider management with primary/legacy support for zero-downtime migration.
/// </para>
/// <para>
/// <strong>REGISTRY PATTERN:</strong> IEncryptionProviderRegistry manages multiple encryption providers.
/// It has 7 methods (most since ServiceRegistry), supports primary/legacy model, uses thread-safe
/// ConcurrentDictionary + Lock, and has a parameterless constructor.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>Register adds provider with unique ID, throws on null/duplicate</description></item>
/// <item><description>GetProvider returns provider or null, case-insensitive, throws on null ID</description></item>
/// <item><description>GetPrimary throws if no primary configured, returns primary otherwise</description></item>
/// <item><description>SetPrimary throws if provider not registered, sets primary otherwise</description></item>
/// <item><description>GetLegacyProviders returns empty list initially</description></item>
/// <item><description>GetAll returns all registered providers</description></item>
/// <item><description>FindDecryptionProvider throws on null encryptedData</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>NO DISPOSAL:</strong> IEncryptionProviderRegistry is NOT IDisposable.
/// Registry manages provider references, not their lifecycle.
/// </para>
/// <para>
/// To use this kit:
/// <list type="number">
/// <item><description>Inherit from this class</description></item>
/// <item><description>Implement <see cref="CreateRegistry"/> to return your IEncryptionProviderRegistry implementation</description></item>
/// <item><description>Implement <see cref="CreateMockProvider"/> to return mock IEncryptionProvider instances</description></item>
/// <item><description>Create [Fact] test methods that call the protected test methods</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyRegistryConformanceTests : EncryptionProviderRegistryConformanceTestKit
/// {
///     protected override IEncryptionProviderRegistry CreateRegistry() =&gt;
///         new MyEncryptionProviderRegistry();
///
///     protected override IEncryptionProvider CreateMockProvider() =&gt;
///         A.Fake&lt;IEncryptionProvider&gt;();
///
///     [Fact]
///     public void Register_ShouldSucceed_Test() =&gt;
///         Register_ShouldSucceed();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class EncryptionProviderRegistryConformanceTestKit
{
	/// <summary>
	/// Creates an instance of the <see cref="IEncryptionProviderRegistry"/> implementation under test.
	/// </summary>
	/// <returns>A new instance of the registry implementation.</returns>
	/// <remarks>
	/// Implementations should return a fresh, empty registry instance.
	/// EncryptionProviderRegistry has a parameterless constructor.
	/// </remarks>
	protected abstract IEncryptionProviderRegistry CreateRegistry();

	/// <summary>
	/// Creates a mock <see cref="IEncryptionProvider"/> for testing.
	/// </summary>
	/// <returns>A mock provider instance.</returns>
	/// <remarks>
	/// Tests require mock providers to register with the registry.
	/// Use FakeItEasy or any other mocking framework.
	/// </remarks>
	protected abstract IEncryptionProvider CreateMockProvider();

	/// <summary>
	/// Gets a unique provider ID for testing.
	/// </summary>
	protected virtual string DefaultProviderId => "test-provider";

	/// <summary>
	/// Gets a secondary provider ID for testing.
	/// </summary>
	protected virtual string SecondaryProviderId => "secondary-provider";

	#region Register Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.Register"/> succeeds with valid parameters.
	/// </summary>
	protected virtual void Register_ShouldSucceed()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider = CreateMockProvider();

		// Act - Should not throw
		registry.Register(DefaultProviderId, provider);

		// Assert - Provider should be retrievable
		var retrieved = registry.GetProvider(DefaultProviderId);
		if (retrieved == null)
		{
			throw new TestFixtureAssertionException(
				"Expected Register to add provider that can be retrieved via GetProvider.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.Register"/> throws on null providerId.
	/// </summary>
	protected virtual void Register_NullProviderId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider = CreateMockProvider();

		// Act & Assert
		try
		{
			registry.Register(null!, provider);
			throw new TestFixtureAssertionException(
				"Expected Register with null providerId to throw ArgumentNullException.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.Register"/> throws on null provider.
	/// </summary>
	protected virtual void Register_NullProvider_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			registry.Register(DefaultProviderId, null!);
			throw new TestFixtureAssertionException(
				"Expected Register with null provider to throw ArgumentNullException.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.Register"/> throws on duplicate providerId.
	/// </summary>
	protected virtual void Register_DuplicateId_ShouldThrowInvalidOperationException()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider1 = CreateMockProvider();
		var provider2 = CreateMockProvider();
		registry.Register(DefaultProviderId, provider1);

		// Act & Assert
		try
		{
			registry.Register(DefaultProviderId, provider2);
			throw new TestFixtureAssertionException(
				"Expected Register with duplicate providerId to throw InvalidOperationException.");
		}
		catch (InvalidOperationException)
		{
			// Expected
		}
	}

	#endregion

	#region GetProvider Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetProvider"/> returns the registered provider.
	/// </summary>
	protected virtual void GetProvider_Registered_ShouldReturnProvider()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider = CreateMockProvider();
		registry.Register(DefaultProviderId, provider);

		// Act
		var retrieved = registry.GetProvider(DefaultProviderId);

		// Assert
		if (retrieved == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetProvider to return the registered provider.");
		}

		if (!ReferenceEquals(retrieved, provider))
		{
			throw new TestFixtureAssertionException(
				"Expected GetProvider to return the same provider instance that was registered.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetProvider"/> returns null for unknown providerId.
	/// </summary>
	protected virtual void GetProvider_Unknown_ShouldReturnNull()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var result = registry.GetProvider("unknown-provider");

		// Assert
		if (result != null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetProvider to return null for unknown providerId.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetProvider"/> throws on null providerId.
	/// </summary>
	protected virtual void GetProvider_NullId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			_ = registry.GetProvider(null!);
			throw new TestFixtureAssertionException(
				"Expected GetProvider with null providerId to throw ArgumentNullException.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	#endregion

	#region GetPrimary Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetPrimary"/> throws when no primary is configured.
	/// </summary>
	protected virtual void GetPrimary_NoPrimary_ShouldThrowInvalidOperationException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			_ = registry.GetPrimary();
			throw new TestFixtureAssertionException(
				"Expected GetPrimary to throw InvalidOperationException when no primary is configured.");
		}
		catch (InvalidOperationException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetPrimary"/> returns the primary provider after SetPrimary.
	/// </summary>
	protected virtual void GetPrimary_WithPrimary_ShouldReturnProvider()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider = CreateMockProvider();
		registry.Register(DefaultProviderId, provider);
		registry.SetPrimary(DefaultProviderId);

		// Act
		var primary = registry.GetPrimary();

		// Assert
		if (primary == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetPrimary to return non-null provider after SetPrimary.");
		}

		if (!ReferenceEquals(primary, provider))
		{
			throw new TestFixtureAssertionException(
				"Expected GetPrimary to return the same provider instance that was set as primary.");
		}
	}

	#endregion

	#region SetPrimary Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.SetPrimary"/> throws on null providerId.
	/// </summary>
	protected virtual void SetPrimary_NullProviderId_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			registry.SetPrimary(null!);
			throw new TestFixtureAssertionException(
				"Expected SetPrimary with null providerId to throw ArgumentNullException.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.SetPrimary"/> throws for unregistered providerId.
	/// </summary>
	protected virtual void SetPrimary_Unregistered_ShouldThrowInvalidOperationException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			registry.SetPrimary("unregistered-provider");
			throw new TestFixtureAssertionException(
				"Expected SetPrimary with unregistered providerId to throw InvalidOperationException.");
		}
		catch (InvalidOperationException)
		{
			// Expected
		}
	}

	#endregion

	#region GetLegacyProviders Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetLegacyProviders"/> returns empty list initially.
	/// </summary>
	protected virtual void GetLegacyProviders_Initially_ShouldBeEmpty()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act
		var legacyProviders = registry.GetLegacyProviders();

		// Assert
		if (legacyProviders == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetLegacyProviders to return non-null list.");
		}

		if (legacyProviders.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected GetLegacyProviders to return empty list initially, but got {legacyProviders.Count} items.");
		}
	}

	#endregion

	#region GetAll Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.GetAll"/> returns all registered providers.
	/// </summary>
	protected virtual void GetAll_WithProviders_ShouldReturnAll()
	{
		// Arrange
		var registry = CreateRegistry();
		var provider1 = CreateMockProvider();
		var provider2 = CreateMockProvider();
		registry.Register(DefaultProviderId, provider1);
		registry.Register(SecondaryProviderId, provider2);

		// Act
		var all = registry.GetAll();

		// Assert
		if (all == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAll to return non-null list.");
		}

		if (all.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected GetAll to return 2 providers, but got {all.Count}.");
		}
	}

	#endregion

	#region FindDecryptionProvider Method Tests

	/// <summary>
	/// Verifies that <see cref="IEncryptionProviderRegistry.FindDecryptionProvider"/> throws on null encryptedData.
	/// </summary>
	protected virtual void FindDecryptionProvider_NullEncryptedData_ShouldThrowArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistry();

		// Act & Assert
		try
		{
			_ = registry.FindDecryptionProvider(null!);
			throw new TestFixtureAssertionException(
				"Expected FindDecryptionProvider with null encryptedData to throw ArgumentNullException.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	#endregion
}
