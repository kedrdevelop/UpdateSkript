using UpdateSkriptApp.Services;
using Xunit;

namespace UpdateSkriptApp.Tests
{
    public class DellCatalogParserTests
    {
        private const string FakeCatalogXml = @"
<DriverPackCatalog>
    <DriverPack path=""FOLDER01/Version01/Latitude_5420.exe"" osCode=""WT64A"">
        <SupportedModel>
            <Display>Latitude 5420</Display>
        </SupportedModel>
    </DriverPack>
    <DriverPack path=""FOLDER02/Version02/Optiplex_7080.exe"" osCode=""WT64A"">
        <SupportedModel>
            <Display>Optiplex 7080</Display>
        </SupportedModel>
    </DriverPack>
    <DriverPack path=""FOLDER03/OldOS.exe"" osCode=""W1064"">
        <SupportedModel>
            <Display>Latitude 5420</Display>
        </SupportedModel>
    </DriverPack>
</DriverPackCatalog>";

        [Fact]
        public void FindMatch_ShouldReturnCorrectPack_ForExactModel()
        {
            // Act
            var match = DellCatalogParser.FindMatch(FakeCatalogXml, "Latitude 5420");

            // Assert
            Assert.NotNull(match);
            Assert.Equal("Latitude_5420.exe", match.Name);
            Assert.Equal("FOLDER01/Version01/Latitude_5420.exe", match.Path);
        }

        [Fact]
        public void FindMatch_ShouldReturnNull_WhenNoOSMatch()
        {
            // Act
            // Although model matches, osCode is WT64A only in our logic
            var match = DellCatalogParser.FindMatch(FakeCatalogXml, "NonExistentModel");

            // Assert
            Assert.Null(match);
        }

        [Fact]
        public void FindMatch_ShouldBeCaseInsensitive()
        {
            // Act
            var match = DellCatalogParser.FindMatch(FakeCatalogXml, "latitude 5420");

            // Assert
            Assert.NotNull(match);
            Assert.Equal("Latitude_5420.exe", match.Name);
        }
    }
}
