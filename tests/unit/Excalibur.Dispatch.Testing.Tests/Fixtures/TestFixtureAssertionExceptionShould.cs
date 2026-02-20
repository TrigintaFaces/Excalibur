// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing;

namespace Excalibur.Dispatch.Testing.Tests.Fixtures;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestFixtureAssertionExceptionShould
{
	[Fact]
	public void StoreMessage()
	{
		var ex = new TestFixtureAssertionException("test message");
		ex.Message.ShouldBe("test message");
	}

	[Fact]
	public void StoreInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new TestFixtureAssertionException("outer", inner);

		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void InheritFromException()
	{
		var ex = new TestFixtureAssertionException("test");
		ex.ShouldBeAssignableTo<Exception>();
	}
}
