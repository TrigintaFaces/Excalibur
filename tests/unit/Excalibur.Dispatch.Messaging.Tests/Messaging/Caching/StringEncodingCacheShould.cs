// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.7 (bd-6vuk4):
/// StringEncodingCache bare catch -> selective exception handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StringEncodingCacheShould : IDisposable
{
	private readonly StringEncodingCache _sut = new(maxCacheSize: 100);

	[Fact]
	public void NotHaveBareCatchInCleanupMethod()
	{
		// Verify the Cleanup method catches specific exceptions, not bare catch
		var cleanupMethod = typeof(StringEncodingCache)
			.GetMethod("Cleanup", BindingFlags.NonPublic | BindingFlags.Instance);

		cleanupMethod.ShouldNotBeNull("Cleanup method should exist");

		// Get the method body and check for exception handler types
		var body = cleanupMethod.GetMethodBody();
		body.ShouldNotBeNull();

		var exceptionHandlers = body.ExceptionHandlingClauses;

		// There should be at least one handler for InvalidOperationException
		// and NOT a bare catch (which would be a Catch clause with null ExceptionType)
		var hasBareCatch = false;
		var hasSpecificCatch = false;

		foreach (var handler in exceptionHandlers)
		{
			if (handler.Flags == ExceptionHandlingClauseOptions.Clause)
			{
				if (handler.CatchType == null || handler.CatchType == typeof(object))
				{
					hasBareCatch = true;
				}
				else
				{
					hasSpecificCatch = true;
				}
			}
		}

		hasBareCatch.ShouldBeFalse("Cleanup should NOT have a bare catch block");
		hasSpecificCatch.ShouldBeTrue("Cleanup should catch specific exception types (e.g., InvalidOperationException)");
	}

	[Fact]
	public void CacheAndRetrieveUtf8Bytes()
	{
		// Arrange
		var testString = "Hello, World!";

		// Act
		var bytes1 = _sut.GetUtf8Bytes(testString);
		var bytes2 = _sut.GetUtf8Bytes(testString);

		// Assert — both should return the same content
		bytes1.SequenceEqual(bytes2).ShouldBeTrue("Cached bytes should match");
	}

	[Fact]
	public void HandleEmptyString()
	{
		// Act
		var bytes = _sut.GetUtf8Bytes(string.Empty);

		// Assert
		bytes.Length.ShouldBe(0);
	}

	public void Dispose() => _sut.Dispose();
}
