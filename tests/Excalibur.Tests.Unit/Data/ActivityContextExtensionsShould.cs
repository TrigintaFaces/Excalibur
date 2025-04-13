using Excalibur.Data;
using Excalibur.Domain;

using Shouldly;

namespace Excalibur.Tests.Unit.Data;

public class ActivityContextExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext!.DomainDb());
	}
}
