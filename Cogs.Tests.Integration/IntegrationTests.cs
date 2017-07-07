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
            string type = "$type";
            //Hamburger hamburger = new Hamburger
            //{
            //    ID = Guid.NewGuid().ToString(),
            //    HamburgerName = "Big Mac",
            //    Description = "Large Special",
            //    Enclosure = ,
            //    Patty = ,
            //    Sauce = ,
            //    CheeseUsed = 

            //};
            Bread bread = new Bread
            {
                ID = "bread123",
                Name = "Bagels",
                Size = new Dimensions
                {
                    Width = 12,
                    Length = 13,
                }

            };
            //Roll roll = new Roll
            //{
            //    ID = Guid.NewGuid().ToString(),
            //    Name = "Sesame seed bun"
            //};

            //MeatPatty meatPatty = new MeatPatty
            //{
            //    ID = Guid.NewGuid().ToString()
            //};

            //MeatPatty meatPatty2 = new MeatPatty
            //{
            //    ID = Guid.NewGuid().ToString()
            //};

            //hamburger.Enclosure = roll;
            //hamburger.Patty.Add(meatPatty);
            //hamburger.Patty.Add(meatPatty2);
            
            ItemContainer container = new ItemContainer();
            //container.TopLevelReferences.Add(hamburger);
            container.Items.Add(bread);
            //container.Items.Add(hamburger);
            //container.Items.Add(roll);
            //container.Items.Add(meatPatty);
            //container.Items.Add(meatPatty2);

            string json = container.Serialize();
            
            string jsonSchema = File.ReadAllText(@"..\..\generated\jsonSchema.json");
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            JsonSchema4 schema = await JsonSchema4.FromJsonAsync(jsonSchema);

            var errors = schema.Validate(json);

            Assert.Empty(errors);
        }
    }
}
