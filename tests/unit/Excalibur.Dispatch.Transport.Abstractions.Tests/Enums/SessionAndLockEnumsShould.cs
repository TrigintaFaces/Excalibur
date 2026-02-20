using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class SessionAndLockEnumsShould
{
    [Theory]
    [InlineData(DispatchSessionState.Active, 0)]
    [InlineData(DispatchSessionState.Idle, 1)]
    [InlineData(DispatchSessionState.Locked, 2)]
    [InlineData(DispatchSessionState.Expired, 3)]
    [InlineData(DispatchSessionState.Closing, 4)]
    [InlineData(DispatchSessionState.Closed, 5)]
    public void DispatchSessionState_Should_Have_Correct_Values(DispatchSessionState state, int expected)
    {
        ((int)state).ShouldBe(expected);
    }

    [Fact]
    public void DispatchSessionState_Should_Have_Six_Values()
    {
        Enum.GetValues<DispatchSessionState>().Length.ShouldBe(6);
    }

    [Theory]
    [InlineData(LockType.Read, 0)]
    [InlineData(LockType.Write, 1)]
    [InlineData(LockType.UpgradeableRead, 2)]
    public void LockType_Should_Have_Correct_Values(LockType lockType, int expected)
    {
        ((int)lockType).ShouldBe(expected);
    }

    [Fact]
    public void LockType_Should_Have_Three_Values()
    {
        Enum.GetValues<LockType>().Length.ShouldBe(3);
    }
}
