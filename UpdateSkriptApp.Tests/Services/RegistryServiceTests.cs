using System;
using FluentAssertions;
using Moq;
using UpdateSkriptApp.Services;
using Xunit;

namespace UpdateSkriptApp.Tests.Services;

public class RegistryServiceTests
{
    private readonly Mock<IFileSystem> _mockFs;
    private readonly Mock<IAppEnvironment> _mockEnv;
    private readonly Mock<IRegistryWrapper> _mockRegistryWrapper;
    private readonly RegistryService _service;

    public RegistryServiceTests()
    {
        _mockFs = new Mock<IFileSystem>();
        _mockEnv = new Mock<IAppEnvironment>();
        _mockRegistryWrapper = new Mock<IRegistryWrapper>();

        _mockEnv.Setup(e => e.GetEnvironmentVariable("PUBLIC")).Returns(@"C:\PublicMock");

        _service = new RegistryService(_mockFs.Object, _mockEnv.Object, _mockRegistryWrapper.Object);
    }

    [Fact]
    public void IsPhaseCompleted_ReturnsTrue_IfFlagFileExists()
    {
        // Arrange
        _mockFs.Setup(f => f.FileExists(@"C:\PublicMock\UpdateSkript_WU.flag")).Returns(true);

        // Act
        var result = _service.IsPhaseCompleted("WU");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPhaseCompleted_ReturnsFalse_IfFlagFileDoesNotExist()
    {
        // Arrange
        _mockFs.Setup(f => f.FileExists(@"C:\PublicMock\UpdateSkript_WU.flag")).Returns(false);

        // Act
        var result = _service.IsPhaseCompleted("WU");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MarkPhaseCompleted_WritesCurrentTimeToFile()
    {
        // Act
        _service.MarkPhaseCompleted("Dell");

        // Assert
        _mockFs.Verify(f => f.WriteAllText(@"C:\PublicMock\UpdateSkript_Dell.flag", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ResetAllFlags_DeletesExistingFlags()
    {
        // Arrange
        _mockFs.Setup(f => f.FileExists(@"C:\PublicMock\UpdateSkript_WU.flag")).Returns(true);
        _mockFs.Setup(f => f.FileExists(@"C:\PublicMock\UpdateSkript_Dell.flag")).Returns(false);

        // Act
        _service.ResetAllFlags();

        // Assert
        _mockFs.Verify(f => f.DeleteFile(@"C:\PublicMock\UpdateSkript_WU.flag"), Times.Once);
        _mockFs.Verify(f => f.DeleteFile(@"C:\PublicMock\UpdateSkript_Dell.flag"), Times.Never);
    }
}
