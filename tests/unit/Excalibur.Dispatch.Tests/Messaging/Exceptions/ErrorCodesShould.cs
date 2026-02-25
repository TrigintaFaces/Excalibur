using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ErrorCodesShould
{
	[Fact]
	public void DefineUnknownError()
	{
		ErrorCodes.UnknownError.ShouldBe("UNK001");
	}

	[Theory]
	[InlineData("CFG001")]
	[InlineData("CFG002")]
	[InlineData("CFG003")]
	[InlineData("CFG004")]
	public void DefineConfigurationCodes(string expected)
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		fields.Select(f => (string)f.GetValue(null)!).ShouldContain(expected);
	}

	[Theory]
	[InlineData("VAL001")]
	[InlineData("VAL002")]
	[InlineData("VAL003")]
	[InlineData("VAL004")]
	[InlineData("VAL005")]
	public void DefineValidationCodes(string expected)
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		fields.Select(f => (string)f.GetValue(null)!).ShouldContain(expected);
	}

	[Theory]
	[InlineData("MSG001")]
	[InlineData("MSG002")]
	[InlineData("MSG003")]
	[InlineData("MSG004")]
	[InlineData("MSG005")]
	[InlineData("MSG006")]
	[InlineData("MSG007")]
	[InlineData("MSG008")]
	public void DefineMessagingCodes(string expected)
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		fields.Select(f => (string)f.GetValue(null)!).ShouldContain(expected);
	}

	[Theory]
	[InlineData("SER001")]
	[InlineData("SER002")]
	[InlineData("SER003")]
	[InlineData("SER004")]
	public void DefineSerializationCodes(string expected)
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		fields.Select(f => (string)f.GetValue(null)!).ShouldContain(expected);
	}

	[Theory]
	[InlineData("SEC001")]
	[InlineData("SEC002")]
	[InlineData("SEC003")]
	[InlineData("SEC004")]
	[InlineData("SEC005")]
	[InlineData("SEC006")]
	public void DefineSecurityCodes(string expected)
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		fields.Select(f => (string)f.GetValue(null)!).ShouldContain(expected);
	}

	[Fact]
	public void DefineResilienceCodes()
	{
		ErrorCodes.ResilienceCircuitBreakerOpen.ShouldBe("RSL001");
		ErrorCodes.ResilienceRetryExhausted.ShouldBe("RSL002");
		ErrorCodes.ResilienceFallbackFailed.ShouldBe("RSL003");
		ErrorCodes.ResilienceBulkheadRejected.ShouldBe("RSL004");
	}

	[Fact]
	public void DefineConcurrencyCodes()
	{
		ErrorCodes.ConcurrencyDeadlock.ShouldBe("CON001");
		ErrorCodes.ConcurrencyRaceCondition.ShouldBe("CON002");
		ErrorCodes.ConcurrencyLockContention.ShouldBe("CON003");
		ErrorCodes.ConcurrencyOptimisticLockFailed.ShouldBe("CON004");
	}

	[Fact]
	public void DefineResultCodes()
	{
		ErrorCodes.ResultUnwrapFailed.ShouldBe("RST001");
	}

	[Fact]
	public void HaveUniqueErrorCodes()
	{
		var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		var codes = fields.Select(f => (string)f.GetValue(null)!).ToList();

		codes.Count.ShouldBe(codes.Distinct().Count());
	}
}
