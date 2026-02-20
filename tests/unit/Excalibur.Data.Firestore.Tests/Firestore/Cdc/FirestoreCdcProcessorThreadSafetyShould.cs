// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore.Cdc;

/// <summary>
/// Tests verifying FirestoreCdcProcessor has volatile _isRunning and _disposed fields (S543.14).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreCdcProcessorThreadSafetyShould : UnitTestBase
{
	[Fact]
	public void HaveVolatileIsRunningField()
	{
		// Arrange
		var field = typeof(FirestoreCdcProcessor)
			.GetField("_isRunning", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("FirestoreCdcProcessor should have _isRunning field");
		field.FieldType.ShouldBe(typeof(bool));

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(IsVolatile),
			"_isRunning field should be marked volatile for thread-safe cross-thread reads");
	}

	[Fact]
	public void HaveVolatileDisposedField()
	{
		// Arrange
		var field = typeof(FirestoreCdcProcessor)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("FirestoreCdcProcessor should have _disposed field");
		field.FieldType.ShouldBe(typeof(bool));

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(IsVolatile),
			"_disposed field should be marked volatile");
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(FirestoreCdcProcessor).GetInterfaces()
			.ShouldContain(typeof(IAsyncDisposable),
				"FirestoreCdcProcessor should implement IAsyncDisposable");
	}
}
