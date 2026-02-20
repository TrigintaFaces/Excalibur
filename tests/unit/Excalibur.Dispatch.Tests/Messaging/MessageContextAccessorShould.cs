using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextAccessorShould
{
	[Fact]
	public void ReturnNullByDefault()
	{
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();

		accessor.MessageContext.ShouldBeNull();
	}

	[Fact]
	public void GetAndSetMessageContext()
	{
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		try
		{
			accessor.MessageContext = context;

			accessor.MessageContext.ShouldBe(context);
		}
		finally
		{
			MessageContextHolder.Clear();
		}
	}

	[Fact]
	public void ReflectContextFromHolder()
	{
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		try
		{
			MessageContextHolder.Current = context;

			accessor.MessageContext.ShouldBe(context);
		}
		finally
		{
			MessageContextHolder.Clear();
		}
	}

	[Fact]
	public void SetContextInHolder()
	{
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		try
		{
			accessor.MessageContext = context;

			MessageContextHolder.Current.ShouldBe(context);
		}
		finally
		{
			MessageContextHolder.Clear();
		}
	}
}
