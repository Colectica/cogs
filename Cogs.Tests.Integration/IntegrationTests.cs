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
            Tuple<int, int> GYM = new Tuple<int, int> (2017, 06 );
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Sesame seed bun",
                Description = "freshly baked daily!",
                Size = new Dimensions { Width =6, Length = 5.00, Height = heights },
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
            hamburger.Patty.Add(meatPatty);
            hamburger.Patty.Add(meatPatty2);

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
            container2.TopLevelReferences.Add(hamburger);
            container2.Items.Add(bread2);
            container2.Items.Add(meatPatty);
            //container 3
            container3.Items.Add(condiment);
            container3.Items.Add(condiment2);

            //container 4
            container4.TopLevelReferences.Add(hamburger2);
            container4.Items.Add(animal);
            container4.Items.Add(veggiePatty);
            container4.Items.Add(veggiePatty2);

            string json = container.Serialize();
            string json2 = container2.Serialize();
            string json3 = container3.Serialize();
            string json4 = container4.Serialize();
            File.WriteAllText(@"C:\Users\clement\Desktop\test.json", json2);
            string jsonSchema = File.ReadAllText(@"..\..\..\..\generated\jsonSchema.json");
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            JsonSchema4 schema = await JsonSchema4.FromJsonAsync(jsonSchema);

            var errors = schema.Validate(json);
            var errors2 = schema.Validate(json2);
            var errors3 = schema.Validate(json3);
            var errors4 = schema.Validate(json4);

            Assert.Empty(errors);
            Assert.Empty(errors2);
            Assert.Empty(errors3);
            Assert.Empty(errors4);

            ItemContainer newContainer = new ItemContainer();
            newContainer.Parse(json);
            errors = schema.Validate(newContainer.Serialize());
            Assert.Empty(errors);
            Assert.Equal(json, newContainer.Serialize());
        }
    }
}