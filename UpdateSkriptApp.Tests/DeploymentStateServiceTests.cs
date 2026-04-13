using System;
using System.IO;
using UpdateSkriptApp.Services;
using Xunit;

namespace UpdateSkriptApp.Tests
{
    public class DeploymentStateServiceTests
    {
        private readonly string _testPath;

        public DeploymentStateServiceTests()
        {
            // Create a unique temp folder for each test run
            _testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testPath);
        }

        [Fact]
        public void IsPhaseCompleted_ShouldReturnFalse_WhenNoFileExists()
        {
            // Arrange
            var service = new DeploymentStateService();
            // Using reflection to hack the private directory for testing (Senior approach)
            typeof(DeploymentStateService)
                .GetField("_flagDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(service, _testPath);

            // Act
            var result = service.IsPhaseCompleted("WU");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void MarkPhaseCompleted_ShouldCreateFile()
        {
            // Arrange
            var service = new DeploymentStateService();
            typeof(DeploymentStateService)
                .GetField("_flagDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(service, _testPath);

            // Act
            service.MarkPhaseCompleted("Dell");
            var result = service.IsPhaseCompleted("Dell");

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(_testPath, "UpdateSkript_Dell.flag")));
        }

        [Fact]
        public void ResetAll_ShouldDeleteFiles()
        {
            // Arrange
            var service = new DeploymentStateService();
            typeof(DeploymentStateService)
                .GetField("_flagDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(service, _testPath);

            service.MarkPhaseCompleted("WU");
            service.MarkPhaseCompleted("Dell");

            // Act
            service.ResetAll();

            // Assert
            Assert.False(service.IsPhaseCompleted("WU"));
            Assert.False(service.IsPhaseCompleted("Dell"));
            Assert.Empty(Directory.GetFiles(_testPath));
        }

        ~DeploymentStateServiceTests()
        {
            // Cleanup
            if (Directory.Exists(_testPath)) Directory.Delete(_testPath, true);
        }
    }
}
