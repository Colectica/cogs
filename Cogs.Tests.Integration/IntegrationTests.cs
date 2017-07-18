using cogsBurger;
using Newtonsoft.Json;
using NJsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                HamburgerName = "Four Corners Burger",
                Date = new DateTime(2017,9,2),
                DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(+1,0,0))
            };

            Hamburger hamburger2 = new Hamburger
            {
                ID = Guid.NewGuid().ToString(),
                Description = "small Special",
                HamburgerName = "Five Corners Burger",
            };

            MultilingualString describe = new MultilingualString
            {
                Language = "eng",
                Content = "Just a normal cow"
            };
            Tuple<int,string> monthG = new Tuple<int, string>(9, "UTC");
            Tuple<int, string> dayG = new Tuple<int, string>(6, "UTC");
            Tuple<int, int, string> mDay = new Tuple<int, int, string>(6, 9, "UTC");
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Cow",
                LingualDescription = new List<MultilingualString> { describe },
                CountryOfOrigin = "USA",
                Date = new DateTime(2017, 6, 9),
                Time = new DateTimeOffset(2017,6,9,2,32,32,new TimeSpan(+1, 0, 0)),
                GMonthDay = mDay
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
            hamburger.Patty = new List<Protein>
            {
                meatPatty,
                meatPatty2
            };
            hamburger2.Patty = new List<Protein> { veggiePatty };

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
            container2.Items.Add(bread2);
            container2.Items.Add(meatPatty);
            //container 3
            container3.Items.Add(condiment);
            container3.Items.Add(condiment2);

            //container 4
            container4.TopLevelReferences.Add(hamburger2);
            container4.Items.Add(hamburger2);
            container4.Items.Add(animal);
            container4.Items.Add(veggiePatty);
            container4.Items.Add(veggiePatty2);
            container4.Items.Add(bread);

            // evaluation
            string schemaPath = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), ".."), ".."), "..");
            string jsonSchema = File.ReadAllText(Path.Combine(Path.Combine(schemaPath, "generated"), "jsonSchema.json"));
            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "out");
            Directory.CreateDirectory(outPath);
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
                File.WriteAllText(Path.Combine(outPath, "serialized" + i + ".json"), json);

                var errors = schema.Validate(json);

                Assert.Empty(errors);

                // test parsing
                ItemContainer newContainer = new ItemContainer();
                newContainer.Parse(json);
                var newJson = newContainer.Serialize();
                File.WriteAllText(Path.Combine(outPath, "parsed" + i + ".json"), newJson);

                errors = schema.Validate(newJson);
                Assert.Empty(errors);

                // check that outputs are the same
                Assert.Equal(json, newJson);
            } 
        }

        [Fact]
        public async void SimpleTypeRoundtripGMonthYear()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new Tuple<int, int, string>(9, 24, "-06:00")
            };
            container.Items.Add(bread);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]
        public async void SimpleTypeRoundtripGMonthYearWithoutTimezone()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new Tuple<int, int, string>(9, 24, "")
            };
            container.Items.Add(bread);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]
        public async void SimpleTypeBoolean()
        {

            ItemContainer container = new ItemContainer();
            Roll roll = new Roll
            {
                ID = Guid.NewGuid().ToString(),
                SesameSeeds = true
            };
            container.Items.Add(roll);


            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Roll>(container2.Items.First());

            Roll roll2 = container2.Items.First() as Roll;
            Assert.Equal(roll.SesameSeeds, roll2.SesameSeeds);
        }

        [Fact]//not working
        public async void SimpleTypeDuration()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Duration = new TimeSpan(10000000)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Duration, animal2.Duration);
        }

        [Fact]
        public async void SimpleTypeDate()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Date = new DateTime(2017, 9, 2)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Date.Date, animal2.Date.Date);
        }

        [Fact]//not working 
        public async void SimpleTypeDateTime()
        {
            ItemContainer container = new ItemContainer();
            Hamburger hamburger = new Hamburger
            {
                ID = Guid.NewGuid().ToString(),
                DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(+1, 0, 0))
            };
            container.Items.Add(hamburger);
            container.TopLevelReferences.Add(hamburger);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Hamburger>(container2.Items.First());

            Hamburger hamburger2 = container2.Items.First() as Hamburger;
            Assert.Equal(hamburger.DateTime, hamburger2.DateTime);
        }

        [Fact]//test fail when timespan is added
        public async void SimpleTypeTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Time = new DateTimeOffset(2017, 6, 9, 2, 32, 32, new TimeSpan())
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Time.TimeOfDay, animal2.Time.TimeOfDay);
        }

        [Fact]
        public async void SimpleTypeGyear()
        {

        }

        [Fact]
        public async void SimpleTypeGMonthDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new Tuple<int, int, string> ( 9, 3, "utc")
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonthDay, animal2.GMonthDay);
        }
        
        [Fact]
        public async void SimpleTypeGDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDay = new Tuple<int, string>(15, "")

            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GDay, animal2.GDay);
        }

        [Fact]
        public async void SimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new Tuple<int, string>(2, "")
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonth, animal2.GMonth);
        }

        [Fact]
        public async void SimpleTypeAnyURI()
        {

        }

        [Fact]
        public async void SimpleTypeLanguage()
        {

        }

        [Fact]
        public async void SimpleTypeCogsDate()
        {

        }

        private async Task<JsonSchema4> GetJsonSchema()
        {
            // TODO build the json schema into the generated assembly as a resource
            string schemaPath = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), ".."), ".."), "..");
            string jsonSchema = File.ReadAllText(Path.Combine(Path.Combine(schemaPath, "generated"), "jsonSchema.json"));

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            JsonSchema4 schema = await JsonSchema4.FromJsonAsync(jsonSchema);
            return schema;
        }
    }
}
