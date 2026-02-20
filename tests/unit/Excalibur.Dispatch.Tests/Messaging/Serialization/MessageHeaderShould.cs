using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageHeaderShould
{
	[Fact]
	public void HaveExpectedMagicValue()
	{
		MessageHeader.MagicValue.ShouldBe(0x45584D53u);
	}

	[Fact]
	public void DefaultToZeros()
	{
		var header = new MessageHeader();

		header.Magic.ShouldBe(0u);
		header.Version.ShouldBe((byte)0);
		header.TypeId.ShouldBe(0u);
		header.PayloadSize.ShouldBe(0);
		header.Timestamp.ShouldBe(0L);
		header.Checksum.ShouldBe(0u);
	}

	[Fact]
	public void AllowSettingAllFields()
	{
		var header = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = 42,
			PayloadSize = 1024,
			Timestamp = 123456789L,
			Checksum = 0xDEADBEEF,
		};

		header.Magic.ShouldBe(MessageHeader.MagicValue);
		header.Version.ShouldBe((byte)1);
		header.TypeId.ShouldBe(42u);
		header.PayloadSize.ShouldBe(1024);
		header.Timestamp.ShouldBe(123456789L);
		header.Checksum.ShouldBe(0xDEADBEEFu);
	}

	[Fact]
	public void SupportEquality()
	{
		var h1 = new MessageHeader { Magic = 1, Version = 2, TypeId = 3, PayloadSize = 4, Timestamp = 5, Checksum = 6 };
		var h2 = new MessageHeader { Magic = 1, Version = 2, TypeId = 3, PayloadSize = 4, Timestamp = 5, Checksum = 6 };
		var h3 = new MessageHeader { Magic = 1, Version = 2, TypeId = 3, PayloadSize = 4, Timestamp = 5, Checksum = 99 };

		h1.Equals(h2).ShouldBeTrue();
		h1.Equals(h3).ShouldBeFalse();
		(h1 == h2).ShouldBeTrue();
		(h1 != h3).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var h = new MessageHeader { Magic = 1, Version = 2 };

		h.Equals((object)new MessageHeader { Magic = 1, Version = 2 }).ShouldBeTrue();
		h.Equals(null).ShouldBeFalse();
		h.Equals("not a header").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var h1 = new MessageHeader { Magic = 1, Version = 2, TypeId = 3, PayloadSize = 4, Timestamp = 5, Checksum = 6 };
		var h2 = new MessageHeader { Magic = 1, Version = 2, TypeId = 3, PayloadSize = 4, Timestamp = 5, Checksum = 6 };

		h1.GetHashCode().ShouldBe(h2.GetHashCode());
	}
}
