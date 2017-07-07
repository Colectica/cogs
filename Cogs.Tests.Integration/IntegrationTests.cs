using cogsBurger;
using Newtonsoft.Json;
using NJsonSchema;
using System;
using System.IO;
using Xunit;

namespace Cogs.Tests.Integration
{
    public class IntegrationTests
    {
        [Fact]
        public async void CsharpWritesValidJson()
        {
            Hamburger hamburger = new Hamburger
            {
                ID = Guid.NewGuid().ToString(),
                Description = "Large Special",
                HamburgerName = "Four Corners Burger"
            };

            Roll roll = new Roll
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Sesame seed bun",
                Description = "freshly baked daily!"
            };

            MeatPatty meatPatty = new MeatPatty
            {
                ID = Guid.NewGuid().ToString()
            };

            MeatPatty meatPatty2 = new MeatPatty
            {
                ID = Guid.NewGuid().ToString()
            };

            hamburger.Enclosure = roll;
            hamburger.Patty.Add(meatPatty);
            hamburger.Patty.Add(meatPatty2);
            
            ItemContainer container = new ItemContainer();
            container.TopLevelReferences.Add(hamburger);

            container.Items.Add(hamburger);
            container.Items.Add(roll);
            container.Items.Add(meatPatty);
            container.Items.Add(meatPatty2);
            
            string json = container.Serialize();

            string jsonSchema = File.ReadAllText(@"..\..\..\..\generated\jsonSchema.json");
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            JsonSchema4 schema = await JsonSchema4.FromJsonAsync(jsonSchema);

            var errors = schema.Validate(json);

            Assert.Empty(errors);

            ItemContainer newContainer = new ItemContainer();
            newContainer.Parse(json);
            errors = schema.Validate(JsonConvert.SerializeObject(newContainer));
            Assert.Empty(errors);
            Assert.Equal(json, JsonConvert.SerializeObject(newContainer));
        }
    }
}
