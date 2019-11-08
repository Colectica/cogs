using Cogs.Dto;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Cogs.Tests
{
    public class CogsLoaderTests
    {
        [Fact]
        public void LoadHamburgerModelTest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger");
            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            // Verify we read all the item types.
            Assert.Equal(10, cogsModel.ItemTypes.Count);
            Assert.Equal(4, cogsModel.ReusableDataTypes.Count);

            // Verify we read all the inheritance correctly.
            var rollType = cogsModel.ItemTypes.First(x => x.Name == "Roll");
            Assert.Equal("Breading", rollType.ExtendsTypeName);

            // Verify we read Topics correctly.
            Assert.Single(cogsModel.TopicIndices);

            // TODO Verify we read properties correctly.

        }
    }
}
