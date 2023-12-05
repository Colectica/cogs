using CogsBurger.Model;
using Newtonsoft.Json;
using NJsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;
using Cogs.SimpleTypes;
using Cogs.DataAnnotations;
using System.Reflection;

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
            GMonth monthG = new GMonth(9, "Z");
            GDay dayG = new GDay(6, "+09:00");
            GMonthDay mDay = new GMonthDay(6, 9, "-12:00");
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

            List<decimal> heights = new List<decimal> { 5, 5 };
            GYearMonth GYM = new GYearMonth(2017, 06, "Z");

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
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                CheeseRumors = new List<LangString>()
                {
                    new LangString("en","A very long time ago"),
                    new LangString("en","In a galaxy far away")
                }
            };
            hamburger.Enclosure = roll;
            hamburger.CheeseUsed = new List<Cheese>() { cheese };
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
            container.Items.Add(cheese);
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
            JsonSchema schema = await GetJsonSchema();
            var containers = new ItemContainer[] { container, container2, container3, container4 };

            for (int i = 0; i < 4; i++)
            {
                // test serializing
                string json = JsonConvert.SerializeObject(containers[i]);

                var errors = schema.Validate(json);

                Assert.Empty(errors);

                // test parsing
                ItemContainer newContainer = JsonConvert.DeserializeObject<ItemContainer>(json);
                var newJson = JsonConvert.SerializeObject(newContainer);

                errors = schema.Validate(newJson);
                Assert.Empty(errors);

                // check that outputs are the same
                Assert.Equal(json, newJson);
            }
        }


        [Fact]
        public async void JsonCreatesOneInstancePerIdentifiedItem()
        {
            Hamburger hamburger = new Hamburger
            {
                ID = Guid.NewGuid().ToString(),
                Description = "Large Special",
                HamburgerName = "Four Corners Burger"                
            };

            MeatPatty beef = new MeatPatty()
            {
                ID = Guid.NewGuid().ToString()
            };

            hamburger.Patty.Add(beef);
            hamburger.Patty.Add(beef);

            ItemContainer container = new ItemContainer();
            container.Items.Add(hamburger);
            container.Items.Add(beef);

            container.TopLevelReferences.Add(hamburger);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.NotEmpty(container2.TopLevelReferences);
            Assert.IsType<Hamburger>(container2.Items[0]);
            Assert.IsType<MeatPatty>(container2.Items[1]);

            Hamburger hamburger2 = container2.Items.First() as Hamburger;
            Assert.True(Object.ReferenceEquals(container2.Items[1], hamburger2.Patty[0]));
            Assert.True(Object.ReferenceEquals(hamburger2.Patty[0], hamburger2.Patty[1]));

            Assert.True(Object.ReferenceEquals(hamburger2, container2.TopLevelReferences[0]));

        }

        [Fact]
        public async void SimpleTypeGMonthYear()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new GYearMonth(9, 24, "-06:00")
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]
        public async void SimpleTypeGMonthYearWithoutTimezone()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new GYearMonth(9, 24, null)
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Gyearmonth, bread2.Gyearmonth);
        }

        [Fact]
        public async void SimpleTypeGMonthYearList()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                GYearMonthList = new List<GYearMonth>()
                {
                    new GYearMonth(9, 24, null),
                    new GYearMonth(12, 93, "+09:00")
                }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.GYearMonthList.Count, bread2.GYearMonthList.Count);
            Assert.Equal(bread.GYearMonthList[0], bread2.GYearMonthList[0]);
            Assert.Equal(bread.GYearMonthList[1], bread2.GYearMonthList[1]);
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


            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Roll>(container2.Items.First());

            Roll roll2 = container2.Items.First() as Roll;
            Assert.Equal(roll.SesameSeeds, roll2.SesameSeeds);
        }

        [Fact]
        public async void SimpleTypeDuration()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Duration = new TimeSpan(10000000)
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Duration, animal2.Duration);
        }

        [Fact]
        public async void SimpleTypeDurationList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Durations = new List<TimeSpan>()
                {
                    new TimeSpan(10000000),
                    new TimeSpan(0)
                }
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Durations.Count, animal2.Durations.Count);
            Assert.Equal(animal.Durations[0], animal.Durations[0]);
            Assert.Equal(animal.Durations[1], animal.Durations[1]);
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

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Date.Value.Date, animal2.Date.Value.Date);
        }

        [Fact]
        public async void SimpleTypeDateList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Dates = new List<DateTimeOffset>()
                {
                    new DateTime(2017, 9, 2),
                    //new DateTime(1,1,1),
                    new DateTime(1562, 8, 23, 5, 12, 46)
                }
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Dates.Count, animal2.Dates.Count);
            for (int i = 0; i < animal.Dates.Count; i++)
            {
                Assert.Equal(animal.Dates[i].Date, animal2.Dates[i].Date);
            }
        }

        [Fact]
        public async void SimpleTypeDateTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(-1, 0, 0))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.DateTime, animal2.DateTime);
        }

        [Fact]
        public async void SimpleTypeDateTimeList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                DateTimes = new List<DateTimeOffset>()
                {
                    new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(+1, 0, 0)),
                    new DateTimeOffset(1,1,1,0,0,0, new TimeSpan())
                }
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.DateTimes.Count, animal2.DateTimes.Count);
            Assert.Equal(animal.DateTimes[0], animal2.DateTimes[0]);
            Assert.Equal(animal.DateTimes[1], animal2.DateTimes[1]);
        }

        [Fact]
        public async void SimpleTypeTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Time = new DateTimeOffset(2017, 6, 9, 2, 32, 32, new TimeSpan())
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.Time.Value.TimeOfDay, animal2.Time.Value.TimeOfDay);
        }

        [Fact]
        public async void SimpleTypeTimeList()
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

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
        public async void SimpleTypeGyear()
        {
            ItemContainer container = new ItemContainer();
            VeggiePatty patty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                GYear = new GYear(9, "Z")
            };
            container.Items.Add(patty);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                GYear = new GYear(9)
            };
            container.Items.Add(patty);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<VeggiePatty>(container2.Items.First());

            VeggiePatty patty2 = container2.Items.First() as VeggiePatty;
            Assert.Equal(patty.GYear, patty2.GYear);
        }

        [Fact]
        public async void SimpleTypeGyearList()
        {
            ItemContainer container = new ItemContainer();
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                Years = new List<GYear>()
                {
                    new GYear(2017, "+09:00"),
                    new GYear(1996, null)
                }
            };
            container.Items.Add(cheese);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Cheese>(container2.Items.First());

            Cheese cheese2 = container2.Items.First() as Cheese;
            Assert.Equal(cheese.Years.Count, cheese2.Years.Count);
            for (int i = 0; i < cheese.Years.Count; i++)
            {
                Assert.Equal(cheese.Years[i], cheese2.Years[i]);
                Assert.Equal(cheese.Years[i], cheese2.Years[i]);
            }
        }

        [Fact]
        public async void SimpleTypeGMonthDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new GMonthDay(9, 3, "Z")
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonthDay, animal2.GMonthDay);
        }

        [Fact]
        public async void SimpleTypeGMonthDayWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new GMonthDay(9, 3, null)
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonthDay, animal2.GMonthDay);
        }

        [Fact]
        public async void SimpleTypeGMonthDayList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDays = new List<GMonthDay>()
                {
                    new GMonthDay(9, 3, null),
                    new GMonthDay(0, 0, "-09:00")
                }
                
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonthDays.Count, animal2.GMonthDays.Count);
            Assert.Equal(animal.GMonthDays[0], animal2.GMonthDays[0]);
            Assert.Equal(animal.GMonthDays[1], animal2.GMonthDays[1]);
        }

        [Fact]
        public async void SimpleTypeGDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDay = new GDay(15, "+10:00")

            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                GDay = new GDay(15, null)

            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GDay, animal2.GDay);
        }

        [Fact]
        public async void SimpleTypeGDayList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDays = new List<GDay>()
                {
                    new GDay(15, null),
                    new GDay(0, "Z")
                }
                

            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GDays.Count, animal2.GDays.Count);
            Assert.Equal(animal.GDays[0], animal2.GDays[0]);
            Assert.Equal(animal.GDays[1], animal2.GDays[1]);
        }

        [Fact]
        public async void SimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new GMonth(2, "+01:00")
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonth, animal2.GMonth);
        }

        [Fact]
        public async void SimpleTypeGMonthWihtoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new GMonth(2, null)
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonth, animal2.GMonth);
        }

        [Fact]
        public async void SimpleTypeGMonthList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonths = new List<GMonth>()
                {
                     new GMonth(2, null),
                     new GMonth(8, "Z")
                }
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.GMonths.Count, animal2.GMonths.Count);
            Assert.Equal(animal.GMonths[0], animal2.GMonths[0]);
            Assert.Equal(animal.GMonths[1], animal2.GMonths[1]);
        }

        [Fact]
        public async void SimpleTypeAnyURI()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                AnyURI = new Uri("http://www.colectica.com/")
            };
            container.Items.Add(condiment);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Condiment>(container2.Items.First());

            Condiment condiment2 = container2.Items.First() as Condiment;
            Assert.Equal(condiment.AnyURI, condiment2.AnyURI);
        }


        [Fact]
        public void DateTimeOffsetSerialization()
        {
            string format = @"yyyy-MM-dd\THH:mm:ss.FFFFFFFK";

            DateTimeOffset offset1 = new DateTimeOffset(2017, 12, 12, 5, 33, 45, 899, new TimeSpan(-4, -30, 0));

            string offsetFormat1 = offset1.ToString(format);
            string json1 = JsonConvert.SerializeObject(offset1);

            Assert.Equal("\"" + offsetFormat1 + "\"", json1);

            DateTimeOffset offset2 = JsonConvert.DeserializeObject<DateTimeOffset>(json1);

            Assert.Equal(offset1, offset2);
        }

        [Fact]
        public async void SimpleTypeString()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                Description = @"My
                                Description"
            };
            container.Items.Add(condiment);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Condiment>(container2.Items.First());

            Condiment condiment2 = container2.Items.First() as Condiment;
            Assert.Equal(condiment.Description, condiment2.Description);
        }

        [Fact]
        public async void SimpleTypeAnyURIList()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                Uris = new List<Uri>
                {
                    new Uri("http://www.colectica.com/"),
                    new Uri("https://github.com/Colectica/cogs")
                }
            };
            container.Items.Add(condiment);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Condiment>(container2.Items.First());

            Condiment condiment2 = container2.Items.First() as Condiment;
            Assert.Equal(condiment.Uris.Count, condiment2.Uris.Count);
            Assert.Equal(condiment.Uris[0], condiment2.Uris[0]);
            Assert.Equal(condiment.Uris[1], condiment2.Uris[1]);
        }

        [Fact]
        public async void SimpleTypeLanguage()
        {
            ItemContainer container = new ItemContainer();
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                Language = "en"
            };
            container.Items.Add(cheese);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Cheese>(container2.Items.First());

            Cheese cheese2 = container2.Items.First() as Cheese;
            Assert.Equal(cheese.Language, cheese2.Language);
        }


        [Fact]
        public async void SimpleTypeLangStringList()
        {
            ItemContainer container = new ItemContainer();
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                CheeseRumors = new List<LangString>()
                {
                    new LangString("en","A very long time ago"),
                    new LangString("en","In a galaxy far away")
                }
            };
            container.Items.Add(cheese);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Cheese>(container2.Items.First());

            Cheese cheese2 = container2.Items.First() as Cheese;
            Assert.Equal(cheese.CheeseRumors.Count, cheese2.CheeseRumors.Count);
            for (int i = 0; i < cheese.Years.Count; i++)
            {
                Assert.Equal(cheese.CheeseRumors[i], cheese2.CheeseRumors[i]);
                Assert.Equal(cheese.CheeseRumors[i], cheese2.CheeseRumors[i]);
            }
        }

        [Fact]
        public async void SimpleTypeLangString()
        {
            ItemContainer container = new ItemContainer();
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Gouda George",
                CheeseBio =  new LangString("en", @"Once there was a cheese from Nantucket named ""Gouda George."" Born amidst the salty breezes of Nantucket's shores, he was the cheesiest character in town—quite literally!
With a mischievous aroma that could charm even the pickiest mice, Gouda George was a rebel among cheeses. He was aged to perfection, soaking up tales from the salty sea captains and whispering cheesy secrets to the seagulls.
He once entered a cheese contest against the famed Cheddar Charles from Chesapeake Bay. The contest was fierce; the stakes were high. Legend has it that as they were being judged, a seagull swooped down and stole the trophy. Gouda George laughed, claiming the seagull simply had impeccable taste.
But despite his cheesy escapades, Gouda George remained a beloved figure in Nantucket. He'd tell tall tales to anyone who'd listen, always adding a dash of humor to his cheesy wisdom.
And as the sun sets over Nantucket, Gouda George stands tall, a cheesy symbol of the island's character, still spinning yarns and making everyone smile with his aged, cheesy wit.")
            };
            container.Items.Add(cheese);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Cheese>(container2.Items.First());

            Cheese cheese2 = container2.Items.First() as Cheese;
            Assert.Equal(cheese.CheeseBio, cheese2.CheeseBio);
        }


        [Fact]
        public async void SimpleTypeCogsDateDateTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new DateTimeOffset(new DateTime(1996, 8, 23, 4, 37, 4), 
                    new TimeSpan(+3, 0, 0)), false)
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new DateTime(2017, 9, 2), true)
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new GYearMonth(2017, 7, "Z"))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new GYearMonth(2017, 7, null))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new GYear(2017, "Z"))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new GYear(2017, null))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
                CDate = new CogsDate(new TimeSpan(1562))
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.Equal(animal.CDate.GetValue(), animal2.CDate.GetValue());
        }

        [Fact]
        public async void SimpleTypeCogsDateList()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                CDates = new List<CogsDate>
                {
                    new CogsDate(new TimeSpan(1562)),
                    new CogsDate(new GYear(2017, "+01:00")),
                    new CogsDate(new DateTimeOffset(new DateTime(1996, 8, 23, 4, 37, 4),
                        new TimeSpan(+3, 0, 0)), false),
                    new CogsDate(new DateTime(2017, 9, 2), true),
                    new CogsDate(new GYearMonth(2017, 7, "+02:00")),
                    new CogsDate(new GYearMonth(2017, 7, null)),
                    new CogsDate(new GYear(2017, null))
                },
                Description = "Dates"
            };
            container.Items.Add(condiment);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Condiment>(container2.Items.First());

            Condiment condiment2 = container2.Items.First() as Condiment;
            Assert.Equal(condiment.CDates.Count, condiment2.CDates.Count);
            for (int i = 0; i < condiment.CDates.Count; i++)
            {
                Assert.Equal(condiment.CDates[i].GetValue(), condiment2.CDates[i].GetValue());
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

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items[1]);

            Bread bread2 = container2.Items[1] as Bread;
            Assert.Equal(bread.Size.Creature.ID, bread2.Size.Creature.ID);
        }

        [Fact]
        public async void ReusabletoSimple()
        {
            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Size = new Dimensions
                {
                    CogsDate = new CogsDate(new TimeSpan(10000000))
                }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.Size.CogsDate.GetValue(), bread2.Size.CogsDate.GetValue());
        }

        [Fact]
        public async void ReusabletoSimpleList()
        {
            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                GYearMonthList = new List<GYearMonth>()
                {
                    new GYearMonth(2017, 7 , null),
                    new GYearMonth(1996, 8, "+01:00")
                }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            Assert.Equal(bread.GYearMonthList.Count, bread2.GYearMonthList.Count);
            Assert.Equal(bread.GYearMonthList[0], bread2.GYearMonthList[0]);
            Assert.Equal(bread.GYearMonthList[1], bread2.GYearMonthList[1]);
        }

        [Fact]
        public async void SubstitutionsInReusableItem()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString()
            };
            container.Items.Add(animal);

            Part sirloin = new Part()
            {
                PartName = "Sirloin"
            };
            animal.MeatPieces.Add(sirloin);
            SubPart Tenderloin = new SubPart()
            {
                PartName = "Tenderloin"
            };
            animal.MeatPieces.Add(Tenderloin);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items.First() as Animal;
            Assert.NotEmpty(animal2.MeatPieces);
            Assert.Equal(animal.MeatPieces.Count, animal2.MeatPieces.Count);
            Assert.IsType<Part>(animal2.MeatPieces[0]);
            Assert.IsType<SubPart>(animal2.MeatPieces[1]);
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

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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
        public async void NestedReusableItemLists()
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
                            },
                            new Part()
                            {
                                PartName = "blood"
                            }
                        }
                    }
                }
            };
            animal.MeatPieces.Add(sirloin);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
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

        [Fact]
        public async void ListOfReusableType()
        {
            ItemContainer container = new ItemContainer();
            MultilingualString describe1 = new MultilingualString
            {
                Content = "This is in english UK",
                Language = "eng-uk"
            };
            MultilingualString describe2 = new MultilingualString
            {
                Content = "This is in english US",
                Language = "eng-sub"
            };
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                LingualDescription = new List<MultilingualString>() {describe1, describe2 }
            };
            container.Items.Add(animal);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);

            Assert.NotEmpty(container2.Items);
            Assert.IsType<Animal>(container2.Items.First());

            Animal animal2 = container2.Items[0] as Animal;
            for(int i = 0; i < animal.LingualDescription.Count; i++)
            {
                Assert.Equal(animal.LingualDescription[0].Content, animal2.LingualDescription[0].Content);
                Assert.Equal(animal.LingualDescription[0].Language, animal2.LingualDescription[0].Language);
            }
        }

        [Fact]
        public async void ListOfSimpleTypeGyear()
        {
            ItemContainer container = new ItemContainer();
            GYear year1 = new GYear(1997, "+09:00");
            GYear year2 = new GYear(2002, "+09:00");
            GYear year3 = new GYear(2017, "Z");
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyear = new List<GYear>() { year1, year2, year3 }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            for(int i = 0; i <  bread.Gyear.Count;i++)
            {
                Assert.Equal(bread.Gyear[i], bread2.Gyear[i]);
            }
        }

        [Fact]
        public async void ListOfSimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            GMonth month1 = new GMonth(6, "Z");
            GMonth month2 = new GMonth(9, "+09:00");
            GMonth month3 = new GMonth(17, "+01:00");
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gmonth = new List<GMonth>() { month1, month2, month3 }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            for (int i = 0; i < bread.Gmonth.Count; i++)
            {
                Assert.Equal(bread.Gmonth[i], bread2.Gmonth[i]);
            }
        }

        [Fact]
        public async void ListOfSimpleTypeGDay()
        {
            ItemContainer container = new ItemContainer();
            GDay day1 = new GDay(1, "Z");
            GDay day2 = new GDay(9, "+09:00");
            GDay day3 = new GDay(12, "-01:00");
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gday = new List<GDay>() { day1, day2, day3 }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            for (int i = 0; i < bread.Gday.Count; i++)
            {
                Assert.Equal(bread.Gday[i], bread2.Gday[i]);
            }
        }

        [Fact]
        public async void ListOfSimpleTypeGMonthDay()
        {
            ItemContainer container = new ItemContainer();
            GMonthDay day1 = new GMonthDay(1, 2, "+09:00");
            GMonthDay day2 = new GMonthDay(9, 12, "Z");
            GMonthDay day3 = new GMonthDay(12, 23, "+00:00");
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new List<GMonthDay>() { day1, day2, day3 }
            };
            container.Items.Add(bread);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Bread>(container2.Items.First());

            Bread bread2 = container2.Items.First() as Bread;
            for (int i = 0; i < bread.Gday.Count; i++)
            {
                Assert.Equal(bread.Gday[i], bread2.Gday[i]);
            }
        }

        [Fact]
        public async void ListOfSimpleTypeGYearMonth()
        {
            ItemContainer container = new ItemContainer();
            GYearMonth ym1 = new GYearMonth(1996, 2, "Z");
            GYearMonth ym2 = new GYearMonth(2002, 9, "+03:00");
            GYearMonth ym3 = new GYearMonth(2017, 12, "+02:00");
            Roll roll = new Roll
            {
                ID = Guid.NewGuid().ToString(),
                GYearMonth = new List<GYearMonth>() { ym1, ym2, ym3 }
            };
            container.Items.Add(roll);

            JsonSchema schema = await GetJsonSchema();
            string json = JsonConvert.SerializeObject(container);
            var errors = schema.Validate(json);
            Assert.Empty(errors);

            ItemContainer container2 = JsonConvert.DeserializeObject<ItemContainer>(json);
            string json2 = JsonConvert.SerializeObject(container2);
            Assert.Equal(json, json2);
            Assert.NotEmpty(container2.Items);
            Assert.IsType<Roll>(container2.Items.First());

            Roll roll2 = container2.Items.First() as Roll;
            for(int i = 0; i < roll.GYearMonth.Count; i++)
            {
                Assert.Equal(roll.GYearMonth[i], roll2.GYearMonth[i]);
            }
        }

        private async Task<JsonSchema> GetJsonSchema()
        {
            string schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..");
            string jsonSchemaFileName = Path.Combine(schemaPath, "generated", "jsonSchema.json");
            string jsonSchema = File.ReadAllText(jsonSchemaFileName);
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None
            };
            JsonSchema schema = await JsonSchema.FromJsonAsync(jsonSchema);
            return schema;
        }
    }
}