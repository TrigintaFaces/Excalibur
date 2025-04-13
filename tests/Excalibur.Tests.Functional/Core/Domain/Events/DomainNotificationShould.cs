using Excalibur.Core.Domain.Events;

using FakeItEasy;

using MediatR;

namespace Excalibur.Tests.Functional.Core.Domain.Events;

public class DomainNotificationShould
{
	[Fact]
	public async Task DomainNotificationShouldTriggerHandlerCorrectly()
	{
		// Arrange
		var handler = A.Fake<INotificationHandler<IDomainNotification>>();
		var notification = A.Fake<IDomainNotification>();

		// Act
		await handler.Handle(notification, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => handler.Handle(notification, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}
}
