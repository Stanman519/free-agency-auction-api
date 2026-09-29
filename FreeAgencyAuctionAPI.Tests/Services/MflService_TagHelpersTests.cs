using System.Linq;
using FreeAgencyAuctionAPI.Services;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class MflService_TagHelpersTests
    {
        [Fact]
        public void ParseTags_NullOrEmpty_ReturnsEmptyList()
        {
            Assert.Empty(MflService.ParseTags(null));
            Assert.Empty(MflService.ParseTags(""));
            Assert.Empty(MflService.ParseTags("  "));
        }

        [Fact]
        public void ParseTags_SplitsOnPipeAndTrims()
        {
            var result = MflService.ParseTags("R1-2024| HOLDOUT |TAG-2");
            Assert.Equal(new[] { "R1-2024", "HOLDOUT", "TAG-2" }, result);
        }

        [Fact]
        public void AddTag_ToEmpty_ReturnsJustThatTag()
        {
            Assert.Equal("HOLDOUT", MflService.AddTag(null, "HOLDOUT"));
        }

        [Fact]
        public void AddTag_ToExisting_Combines()
        {
            Assert.Equal("R1-2024|HOLDOUT", MflService.AddTag("R1-2024", "HOLDOUT"));
        }

        [Fact]
        public void AddTag_AlreadyPresent_DoesNotDuplicate()
        {
            Assert.Equal("HOLDOUT", MflService.AddTag("HOLDOUT", "HOLDOUT"));
        }

        [Fact]
        public void RemoveTag_RemovesOnlyThatTag_PreservesOthers()
        {
            Assert.Equal("R1-2024", MflService.RemoveTag("R1-2024|HOLDOUT", "HOLDOUT"));
        }

        [Fact]
        public void RemoveTag_LastRemainingTag_ReturnsNull()
        {
            Assert.Null(MflService.RemoveTag("HOLDOUT", "HOLDOUT"));
        }

        [Fact]
        public void RemoveTag_NotPresent_ReturnsUnchanged()
        {
            Assert.Equal("R1-2024", MflService.RemoveTag("R1-2024", "HOLDOUT"));
        }

        [Fact]
        public void ReplaceTagsWithPrefix_SupersedesOldRoundYearTag()
        {
            Assert.Equal("5YO", MflService.ReplaceTagsWithPrefix("R1-2022", "R", "5YO"));
        }

        [Fact]
        public void ReplaceTagsWithPrefix_PreservesUnrelatedTags()
        {
            var result = MflService.ReplaceTagsWithPrefix("R1-2022|HOLDOUT", "R", "5YO");
            var tags = result.Split('|');
            Assert.Contains("5YO", tags);
            Assert.Contains("HOLDOUT", tags);
            Assert.DoesNotContain("R1-2022", tags);
        }

        [Fact]
        public void ReplaceTagsWithPrefix_TagOrdinal_SupersedesOldCount()
        {
            Assert.Equal("TAG-2", MflService.ReplaceTagsWithPrefix("TAG-1", "TAG-", "TAG-2"));
        }
    }
}
