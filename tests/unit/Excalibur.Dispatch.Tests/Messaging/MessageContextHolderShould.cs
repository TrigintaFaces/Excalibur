using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextHolderShould
{
	[Fact]
	public void ReturnNullByDefault()
	{
		MessageContextHolder.Clear();

		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public void SetAndGetCurrentContext()
	{
		var context = A.Fake<IMessageContext>();

		try
		{
			MessageContextHolder.Current = context;

			MessageContextHolder.Current.ShouldBe(context);
		}
		finally
		{
			MessageContextHolder.Clear();
		}
	}

	[Fact]
	public void ClearContext()
	{
		var context = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context;

		MessageContextHolder.Clear();

		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingToNull()
	{
		MessageContextHolder.Current = A.Fake<IMessageContext>();

		MessageContextHolder.Current = null;

		MessageContextHolder.Current.ShouldBeNull();
	}
}
