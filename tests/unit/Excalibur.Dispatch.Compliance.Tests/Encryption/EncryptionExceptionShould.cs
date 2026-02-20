using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionExceptionShould
{
	[Fact]
	public void Create_with_default_constructor()
	{
		var ex = new EncryptionException();

		ex.Message.ShouldNotBeNullOrWhiteSpace();
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.Unknown);
		ex.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Create_with_message()
	{
		var ex = new EncryptionException("test message");

		ex.Message.ShouldBe("test message");
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.Unknown);
	}

	[Fact]
	public void Create_with_message_and_inner_exception()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new EncryptionException("test message", inner);

		ex.Message.ShouldBe("test message");
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void Allow_setting_error_code_via_init()
	{
		var ex = new EncryptionException("test") { ErrorCode = EncryptionErrorCode.KeyNotFound };

		ex.ErrorCode.ShouldBe(EncryptionErrorCode.KeyNotFound);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DecryptionExceptionShould
{
	[Fact]
	public void Create_with_default_constructor()
	{
		var ex = new DecryptionException();

		ex.Message.ShouldNotBeNullOrWhiteSpace();
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
	}

	[Fact]
	public void Create_with_message()
	{
		var ex = new DecryptionException("decryption failed");

		ex.Message.ShouldBe("decryption failed");
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
	}

	[Fact]
	public void Create_with_message_and_inner_exception()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new DecryptionException("decryption failed", inner);

		ex.Message.ShouldBe("decryption failed");
		ex.InnerException.ShouldBeSameAs(inner);
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
	}

	[Fact]
	public void Create_with_message_and_error_code()
	{
		var ex = new DecryptionException("auth failed", EncryptionErrorCode.AuthenticationFailed);

		ex.Message.ShouldBe("auth failed");
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.AuthenticationFailed);
	}

	[Fact]
	public void Create_with_message_inner_exception_and_error_code()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new DecryptionException("auth failed", inner, EncryptionErrorCode.AuthenticationFailed);

		ex.Message.ShouldBe("auth failed");
		ex.InnerException.ShouldBeSameAs(inner);
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.AuthenticationFailed);
	}

	[Fact]
	public void Inherit_from_encryption_exception()
	{
		var ex = new DecryptionException("test");

		ex.ShouldBeAssignableTo<EncryptionException>();
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionErrorCodeShould
{
	[Theory]
	[InlineData(EncryptionErrorCode.Unknown, 0)]
	[InlineData(EncryptionErrorCode.KeyNotFound, 1)]
	[InlineData(EncryptionErrorCode.KeyExpired, 2)]
	[InlineData(EncryptionErrorCode.KeySuspended, 3)]
	[InlineData(EncryptionErrorCode.InvalidCiphertext, 4)]
	[InlineData(EncryptionErrorCode.AuthenticationFailed, 5)]
	[InlineData(EncryptionErrorCode.FipsComplianceViolation, 6)]
	[InlineData(EncryptionErrorCode.AccessDenied, 7)]
	[InlineData(EncryptionErrorCode.ServiceUnavailable, 8)]
	[InlineData(EncryptionErrorCode.UnsupportedAlgorithm, 9)]
	public void Have_expected_integer_values(EncryptionErrorCode code, int expectedValue)
	{
		((int)code).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_ten_values()
	{
		Enum.GetValues<EncryptionErrorCode>().Length.ShouldBe(10);
	}
}
