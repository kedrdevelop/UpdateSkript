using FluentAssertions;
using Moq;
using System;
using System.Threading.Tasks;
using UpdateSkriptApp.Modules;
using UpdateSkriptApp.Services;
using Xunit;

namespace UpdateSkriptApp.Tests.Modules;

public class WinUpdateProviderTests
{
    private readonly Mock<IPowerShellRunner> _mockPowerShell;
    private readonly Mock<IUpdateHistoryTracker> _mockHistory;
    private readonly Mock<IFileSystem> _mockFs;
    private readonly Mock<IAppEnvironment> _mockEnv;
    private readonly WinUpdateProvider _provider;

    public WinUpdateProviderTests()
    {
        _mockPowerShell = new Mock<IPowerShellRunner>();
        _mockHistory = new Mock<IUpdateHistoryTracker>();
        _mockFs = new Mock<IFileSystem>();
        _mockEnv = new Mock<IAppEnvironment>();
        
        _mockEnv.Setup(e => e.TempDirectory).Returns(@"C:\Temp");

        _provider = new WinUpdateProvider(_mockPowerShell.Object, _mockHistory.Object, _mockFs.Object, _mockEnv.Object);
    }

    [Fact]
    public async Task RunWindowsUpdatesAsync_ReturnsFalse_WhenNoUpdatesJSON()
    {
        // Assemble
        _mockPowerShell.Setup(p => p.ExecuteScriptAsync(It.IsAny<string>(), true, It.IsAny<Action<string>>()))
            .ReturnsAsync((0, ""));
            
        _mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFs.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns("[]");

        // Act
        var result = await _provider.RunWindowsUpdatesAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RunWindowsUpdatesAsync_ReturnsTrue_WhenUpdatesInstalledAndRebootRequired()
    {
        // Assemble
        _mockPowerShell.Setup(p => p.ExecuteScriptAsync(It.Is<string>(s => s.Contains("Get-WindowsUpdate")), true, It.IsAny<Action<string>>()))
            .ReturnsAsync((0, ""));
            
        _mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFs.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(@"[{""Title"":""Some Update"",""KB"":""KB123456""}]");

        _mockHistory.Setup(h => h.IsSkippedOrInstalled("KB123456")).Returns(false);
        _mockHistory.Setup(h => h.GetAttempts("KB123456")).Returns(0);

        _mockPowerShell.Setup(p => p.ExecuteScriptAsync(It.Is<string>(s => s.Contains("Install-WindowsUpdate")), true, It.IsAny<Action<string>>()))
            .ReturnsAsync((0, "INSTALL_SUCCESS"));

        // Act
        var result = await _provider.RunWindowsUpdatesAsync();

        // Assert
        result.Should().BeTrue();
    }
}
