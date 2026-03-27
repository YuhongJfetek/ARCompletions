using System.Collections.Generic;
using ARCompletions.Services;
using Xunit;

namespace ARCompletions.Tests
{
    public class PlatformPermissionHelperTests
    {
        [Fact]
        public void ComputeDiffs_AddsAndRemovesCorrectly()
        {
            var existing = new[] { "p1", "p2", "p3" };
            var desired = new[] { "p2", "p4" };

            var (toAdd, toRemove) = PlatformPermissionHelper.ComputeDiffs(existing, desired);

            Assert.Contains("p4", toAdd);
            Assert.DoesNotContain("p2", toAdd);
            Assert.Contains("p1", toRemove);
            Assert.Contains("p3", toRemove);
            Assert.DoesNotContain("p2", toRemove);
        }
    }
}
