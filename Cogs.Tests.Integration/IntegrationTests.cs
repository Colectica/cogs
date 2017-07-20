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
                Date = new DateTime(2017, 9, 2),
                DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(+1, 0, 0))
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
            Tuple<int, string> monthG = new Tuple<int, string>(9, "UTC");
            Tuple<int, string> dayG = new Tuple<int, string>(6, "UTC");
            Tuple<int, int, string> mDay = new Tuple<int, int, string>(6, 9, "UTC");
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Cow",
                LingualDescription = new List<MultilingualString> { describe },
                CountryOfOrigin = "USA",
                Date = new DateTime(2017, 6, 9),
                Time = new DateTimeOffset(2017, 6, 9, 2, 32, 32, new TimeSpan(+1, 0, 0)),
                GMonthDay = mDay
            };

            List<decimal> heights = new List<decimal>();
            heights.Add(5);
            heights.Add(5);
            Tuple<int, int, string> GYM = new Tuple<int, int, string>(2017, 06, "utc");

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

        [Fact]//PASS
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

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]//PASS
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

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]//PASS
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

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Roll>(container2.Items.First());

            Roll roll2 = container2.Items.First() as Roll;
            Assert.Equal(roll.SesameSeeds, roll2.SesameSeeds);
        }

        [Fact]//not working -tojson change to .ticks instead of millisecond, Name and CountryofOrigin cannot be null
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
            string json2 = container2.Serialize();
            Assert.Equal(json, json2);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Duration, animal2.Duration);
        }

        [Fact]//PASS
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

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Date.Date, animal2.Date.Date);
        }

        [Fact]//not working 
        public async void SimpleTypeDateTime()
        {
            ItemContainer container = new ItemContainer();
            Animal hamburger = new Animal
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

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal hamburger2 = container2.Items.First() as Animal;
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
            Assert.Empty(errors); // this is validating online at http://www.jsonschemavalidator.net/

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Time.TimeOfDay, animal2.Time.TimeOfDay);
        }

        [Fact]
        public async void SimpleTypeGyear()
        {
            ItemContainer container = new ItemContainer();
            VeggiePatty patty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                GYear = new Tuple<int, string>(9, "utc")
            };
            container.Items.Add(patty);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<VeggiePatty>(container2.Items.First());

            VeggiePatty patty2 = container2.Items.First() as VeggiePatty;
            Assert.Equal(patty.GYear, patty2.GYear);
        }

        [Fact]
        public async void SimpleTypeGyearWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            VeggiePatty patty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                GYear = new Tuple<int, string>(9, null)
            };
            container.Items.Add(patty);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<VeggiePatty>(container2.Items.First());

            VeggiePatty patty2 = container2.Items.First() as VeggiePatty;
            Assert.Equal(patty.GYear, patty2.GYear);
        }

        [Fact]//PASS
        public async void SimpleTypeGMonthDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new Tuple<int, int, string>(9, 3, "utc")
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonthDay, animal2.GMonthDay);
        }

        [Fact]//PASS
        public async void SimpleTypeGMonthDayWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new Tuple<int, int, string>(9, 3, null)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

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
                GDay = new Tuple<int, string>(15, "utc")

            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GDay, animal2.GDay);
        }

        [Fact]
        public async void SimpleTypeGDayWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDay = new Tuple<int, string>(15, null)

            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GDay, animal2.GDay);
        }

        [Fact]//PASS
        public async void SimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new Tuple<int, string>(2, "utc")
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonth, animal2.GMonth);
        }

        [Fact]//PASS
        public async void SimpleTypeGMonthWihtoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new Tuple<int, string>(2, null)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonth, animal2.GMonth);
        }

        [Fact]
        public async void SimpleTypeAnyURI()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new TimeSpan(1562))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeLanguage()
        {

        }

        [Fact]
        public async void SimpleTypeCogsDateDateTime()
        {
            ItemContainer container = new ItemContainer();
//            VeggiePatty patty = new VeggiePatty
//            {
//                ID = Guid.NewGuid().ToString(),
//                Cogsdate =new DataAnnotations.CogsDate
//                {
//                    Date = new DateTime(2017, 9, 2),
//                    DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(+1, 0, 0)),
//                    GYearMonth = new Tuple<int, int, string>(2017, 9, "utc"),
//                    GYear = new Tuple<int, string>(2017, "utc")

//                }
//            };
//            container.Items.Add(patty);

//            JsonSchema4 schema = await GetJsonSchema();
//            string json = container.Serialize();
//            var errors = schema.Validate(json);
//            Assert.Empty(errors);

//            ItemContainer container2 = new ItemContainer();
//            container2.Parse(json);

//            string json2 = container2.Serialize();
//            Assert.Equal(json, json2);

//            Assert.NotEmpty(container2.Items);
//            Assert.IsType<VeggiePatty>(container2.Items.First());

//            VeggiePatty patty2 = container2.Items.First() as VeggiePatty;
//            Assert.Equal(patty.Cogsdate, patty2.Cogsdate);
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new DateTimeOffset(new DateTime(1996, 8, 23, 4, 37, 4), 
                    new TimeSpan(+3, 0, 0)), false)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateDate()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new DateTime(2017, 9, 2), true)
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateYearMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new Tuple<int, int, string>(2017, 7, "utc"))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateYearMonthNoTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new Tuple<int, int, string>(2017, 7, null))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateYear()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new Tuple<int, string>(2017, "utc"))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateYearNoTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new Tuple<int, string>(2017, null))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateDuration()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new DataAnnotations.CogsDate(new TimeSpan(1562))
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleListTimes()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Times = new List<DateTimeOffset>
                {
                    new DateTimeOffset(2017, 6, 9, 2, 32, 32, new TimeSpan()),
                    new DateTimeOffset(1996, 8, 23, 4, 32, 3, new TimeSpan())
                }
            };
            container.Items.Add(animal);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors); // this is validating online at http://www.jsonschemavalidator.net/

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Times.Count, animal2.Times.Count);
            for (int i = 0; i < animal.Times.Count; i++)
            {
                Assert.Equal(animal.Times[i].TimeOfDay, animal2.Times[i].TimeOfDay);
            }
        }

        [Fact]
        public async void ReusableToItem()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString()
            };
            container.Items.Add(animal);
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Size = new Dimensions
                {
                    Creature = animal
                }
            };
            container.Items.Add(bread);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items[1]);

            Bread bread2 = container2.Items[1] as Bread;
            Assert.Equal(bread.Size.Creature.ID, bread2.Size.Creature.ID);
        }

        [Fact]
        public async void NestedReusableItem()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString()
            };
            container.Items.Add(animal);

            Part sirloin = new Part()
            {
                PartName = "Sirloin",
                SubComponents = new List<Part>()
                {
                    new Part()
                    {
                        PartName = "Tenderloin",
                        SubComponents = new List<Part>()
                        {
                            new Part()
                            {
                                PartName = "marbled wagyu"
                            }
                        }
                    }
                }
            };
            animal.MeatPieces.Add(sirloin);

            JsonSchema4 schema = await GetJsonSchema();
            string json = container.Serialize();
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = new ItemContainer();
            container2.Parse(json);

            string json2 = container2.Serialize();
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.NotEmpty(animal2.MeatPieces);
            Assert.NotNull(animal2.MeatPieces[0]);
            Assert.NotEmpty(animal2.MeatPieces[0].SubComponents);
            Assert.NotNull(animal2.MeatPieces[0].SubComponents[0]);
            Assert.NotEmpty(animal2.MeatPieces[0].SubComponents[0].SubComponents);
            Assert.NotNull(animal2.MeatPieces[0].SubComponents[0].SubComponents[0]);
        }

        [Fact]
        public void ListsInReusableItemsAreInitialized()
        {
            Part sirloin = new Part();            
            Assert.NotNull(sirloin.SubComponents);
        }

        [Fact]
        public void ListsInItemsAreInitialized()
        {
            Animal cow = new Animal();
            Assert.NotNull(cow.Times);
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