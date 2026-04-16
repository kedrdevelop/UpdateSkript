using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UpdateSkriptApp.Modules;
using UpdateSkriptApp.Services;
using Xunit;

namespace UpdateSkriptApp.Tests.Modules;

public class WinUpdateProviderTests
{
    private readonly Mock<IPowerShellRunner> _mockPowerShell;
    private readonly WinUpdateProvider _provider;

    public WinUpdateProviderTests()
    {
        _mockPowerShell = new Mock<IPowerShellRunner>();
        _provider = new WinUpdateProvider(_mockPowerShell.Object);
    }

    [Fact]
    public async Task RunWindowsUpdatesAsync_ReturnsFalse_WhenNoUpdatesInstalled()
    {
        // Arrange
        _mockPowerShell.Setup(p => p.ExecuteScriptAsync(It.IsAny<string>(), true))
            .ReturnsAsync((0, "Some logs... INSTALLCOUNT=0\nEnd of output"));

        // Act
        var result = await _provider.RunWindowsUpdatesAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RunWindowsUpdatesAsync_ReturnsTrue_WhenUpdatesNeedReboot()
    {
        // Arrange
        _mockPowerShell.Setup(p => p.ExecuteScriptAsync(It.IsAny<string>(), true))
            .ReturnsAsync((0, "INSTALLCOUNT=3"));

        // Act
        var result = await _provider.RunWindowsUpdatesAsync();

        // Assert
        result.Should().BeTrue();
    }
}
