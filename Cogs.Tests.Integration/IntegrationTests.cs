using cogsBurger;
using Newtonsoft.Json;
using NJsonSchema;
using System;
using System.Collections.Generic;
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

            Hamburger hamburger2 = new Hamburger
            {
                ID = Guid.NewGuid().ToString(),
                Description = "small Special",
                HamburgerName = "Five Corners Burger"
            };

            MultilingualString describe = new MultilingualString
            {
                Language = "eng",
                Content = "Just a normal cow"
            };

            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Cow",
                LingualDescription = new List<MultilingualString> { describe },
                CountryOfOrigin = "USA",
                Date = new DateTime(2017, 6, 9)

            };

            List<decimal> heights = new List<decimal>();
            heights.Add(5);
            heights.Add(5);
            Tuple<int, int, string> GYM = new Tuple<int, int, string> (2017, 06, "utc");

            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Sesame seed bun",
                Description = "freshly baked daily!",
                Size = new Dimensions { Width = 6, Length = 5.00, Height = heights },
                Gyearmonth = GYM
            };

            Bread bread2 = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Sepcial Bun",
                Description = " a special bun never had before!",
                Size = new Dimensions { Width = 5, Length = 5.00, Height = heights },
                Gyearmonth = GYM
            };

            Roll roll = new Roll
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Sesame seed bun",
                Description = "A nice bun"
            };

            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                Name = "mustard",
                Description = "special mustard",
                IsSpecial = true
            };

            Condiment condiment2 = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                Name = "ketchup",
                Description = "normal ketchup",
                IsSpecial = false
            };

            MeatPatty meatPatty = new MeatPatty
            {
                ID = Guid.NewGuid().ToString()
            };

            MeatPatty meatPatty2 = new MeatPatty
            {
                ID = Guid.NewGuid().ToString()
            };

            VeggiePatty veggiePatty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                VegetableUsed = new List<string> { "red beans", "black beans" }
            };

            VeggiePatty veggiePatty2 = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                VegetableUsed = new List<string> { "garbonzo beans" }
            };

            hamburger.Enclosure = roll;
            hamburger2.Enclosure = bread;
            hamburger.Patty.Add(meatPatty);
            hamburger.Patty.Add(meatPatty2);
            hamburger2.Patty.Add(veggiePatty);

            ItemContainer container = new ItemContainer();
            ItemContainer container2 = new ItemContainer();
            ItemContainer container3 = new ItemContainer();
            ItemContainer container4 = new ItemContainer();

            //container
            container.TopLevelReferences.Add(hamburger);
            container.Items.Add(bread);
            container.Items.Add(hamburger);
            container.Items.Add(roll);
            container.Items.Add(meatPatty);
            container.Items.Add(meatPatty2);
            //container 2
          //  container2.TopLevelReferences.Add(hamburger); <- this references an object not in the model: add hamburger to container
            container2.Items.Add(bread2);
            container2.Items.Add(meatPatty);
            //container 3
            container3.Items.Add(condiment);
            container3.Items.Add(condiment2);

            //container 4
            container4.TopLevelReferences.Add(hamburger2); // <- this references an object not in the model: add hamburger to container
            container4.Items.Add(hamburger2); // now hamburger2 is part of model and can be referenced
            container4.Items.Add(animal);
            container4.Items.Add(veggiePatty);
            container4.Items.Add(veggiePatty2);
            container4.Items.Add(bread);

            // evaluation
            string schemaPath = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), ".."), ".."), "..");
            string jsonSchema = File.ReadAllText(Path.Combine(Path.Combine(schemaPath, "generated"), "jsonSchema.json"));

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            JsonSchema4 schema = await JsonSchema4.FromJsonAsync(jsonSchema);
            var containers = new ItemContainer[] { container, container2, container3, container4 };

            for (int i = 0; i < 4; i++)
            {
                // test serializing
                string json = containers[i].Serialize();
                File.WriteAllText(@"C:\Users\kevin\Documents\jsonOutput\serialized" + i + ".json", json);
                var errors = schema.Validate(json);
                Assert.Empty(errors);

                // test parsing
                ItemContainer newContainer = new ItemContainer();
                newContainer.Parse(json);
                var newJson = newContainer.Serialize();
                File.WriteAllText(@"C:\Users\kevin\Documents\jsonOutput\deserialized" + i + ".json", newJson);
                errors = schema.Validate(newJson);
                Assert.Empty(errors);

                // check that outputs are the same
                Assert.Equal(json, newJson);
            }

        }
    }
}