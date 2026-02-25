// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Testing;

/// <summary>
/// Exception thrown when an assertion in <see cref="AggregateTestFixture{TAggregate}"/> or <see cref="SagaTestFixture{TSaga, TSagaState}"/> fails.
/// </summary>
/// <remarks>
/// This exception is test-framework-agnostic and can be caught by xUnit, NUnit, MSTest, or any other test framework.
/// </remarks>
public sealed class TestFixtureAssertionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TestFixtureAssertionException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the assertion failure.</param>
	public TestFixtureAssertionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TestFixtureAssertionException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the assertion failure.</param>
	/// <param name="innerException">The exception that caused the current exception.</param>
	public TestFixtureAssertionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
