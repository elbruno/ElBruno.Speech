using ElBruno.Speech.AspNetCore;
using FluentAssertions;

namespace ElBruno.Speech.AspNetCore.Tests;

public sealed class SpeechSessionRegistryTests
{
    [Fact]
    public void ActiveSessionCount_StartsAtZero()
    {
        var registry = new SpeechSessionRegistry();
        registry.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public void GetSessionIds_WhenEmpty_ReturnsEmptyList()
    {
        var registry = new SpeechSessionRegistry();
        registry.GetSessionIds().Should().BeEmpty();
    }
}
