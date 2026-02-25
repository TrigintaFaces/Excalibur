// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Exceptions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResourceAlreadyExistsExceptionShould
{
	[Fact]
	public void CreateWithResourceKeyAndResource()
	{
		var ex = new ResourceAlreadyExistsException("key-123", "Order");

		ex.ResourceKey.ShouldBe("key-123");
		ex.Resource.ShouldBe("Order");
		ex.StatusCode.ShouldBe(ResourceAlreadyExistsException.DefaultStatusCode);
		ex.Message.ShouldBe(ResourceAlreadyExistsException.DefaultMessage);
	}

	[Fact]
	public void CreateWithCustomStatusCodeAndMessage()
	{
		var ex = new ResourceAlreadyExistsException("key-123", "Order", 409, "Already exists");

		ex.ResourceKey.ShouldBe("key-123");
		ex.StatusCode.ShouldBe(409);
		ex.Message.ShouldBe("Already exists");
	}

	[Fact]
	public void CreateWithInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ResourceAlreadyExistsException("key", "Order", innerException: inner);

		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithResourceAndStatusCode()
	{
		var ex = new ResourceAlreadyExistsException("Order", 409, "Conflict");

		ex.Resource.ShouldBe("Order");
		ex.StatusCode.ShouldBe(409);
		ex.Message.ShouldBe("Conflict");
	}

	[Fact]
	public void HaveDefaultStatusCode404()
	{
		ResourceAlreadyExistsException.DefaultStatusCode.ShouldBe(404);
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		ResourceAlreadyExistsException.DefaultMessage.ShouldNotBeNullOrWhiteSpace();
	}
}
