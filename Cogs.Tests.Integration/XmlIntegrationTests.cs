using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;
using Cogs.SimpleTypes;
using CogsBurger.Model;
using Cogs.DataAnnotations;

namespace Cogs.Tests.Integration
{
    public class XmlIntegrationTests
    {

        [Fact]
        public void CsharpWritesValidXml()
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
            var containers = new ItemContainer[] { container, container2, container3, container4 };
            for (int i = 0; i < 4; i++)
            {
                XmlValidation(containers[i].MakeXml());
            }
        }

        [Fact]
        public void SimpleTypeGYearMonth()
        {
            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new GYearMonth(2009, 8, "-06:00")
            };
            container.Items.Add(bread);
            
            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGYearMonthWithoutTimezone()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new GYearMonth(9, 12, null)
            };
            container.Items.Add(bread);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGYearMonthList()
        {

            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                GYearMonthList = new List<GYearMonth>()
                {
                    new GYearMonth(9, 12, null),
                    new GYearMonth(2093, 6, "+09:00")
                }
            };
            container.Items.Add(bread);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeBoolean()
        {

            ItemContainer container = new ItemContainer();
            Roll roll = new Roll
            {
                ID = Guid.NewGuid().ToString(),
                SesameSeeds = true
            };
            container.Items.Add(roll);


            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDuration()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Duration = new TimeSpan(10000000)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDurationList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDate()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Date = new DateTime(2017, 9, 2)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDateList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDateTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                DateTime = new DateTimeOffset(new DateTime(2017, 9, 2, 13, 23, 32), new TimeSpan(-1, 0, 0))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeDateTimeList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                Time = new DateTimeOffset(2017, 6, 9, 2, 32, 32, new TimeSpan())
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeTimeList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGyear()
        {
            ItemContainer container = new ItemContainer();
            VeggiePatty patty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                GYear = new GYear(9, "Z")
            };
            container.Items.Add(patty);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGyearWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            VeggiePatty patty = new VeggiePatty
            {
                ID = Guid.NewGuid().ToString(),
                GYear = new GYear(9)
            };
            container.Items.Add(patty);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGyearList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonthDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new GMonthDay(9, 3, "Z")
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonthDayWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDay = new GMonthDay(9, 3, null)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonthDayList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonthDays = new List<GMonthDay>()
                {
                    new GMonthDay(9, 3, null),
                    new GMonthDay(2, 29, "-09:00")
                }

            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGDay()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDay = new GDay(15, "+10:00")

            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGDayWithoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDay = new GDay(15, null)

            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGDayList()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GDays = new List<GDay>()
                {
                    new GDay(15, null),
                    new GDay(1, "Z")
                }


            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new GMonth(2, "+01:00")
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonthWihtoutTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                GMonth = new GMonth(2, null)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeGMonthList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeAnyURI()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                AnyURI = new Uri("http://www.colectica.com/")
            };
            container.Items.Add(condiment);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeString()
        {
            ItemContainer container = new ItemContainer();
            Condiment condiment = new Condiment
            {
                ID = Guid.NewGuid().ToString(),
                Description = @"My
                                Description"
            };
            container.Items.Add(condiment);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeAnyURIList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeLanguage()
        {
            ItemContainer container = new ItemContainer();
            Cheese cheese = new Cheese
            {
                ID = Guid.NewGuid().ToString(),
                Language = "en"
            };
            container.Items.Add(cheese);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateDateTime()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new DateTimeOffset(new DateTime(1996, 8, 23, 4, 37, 4),
                    new TimeSpan(+3, 0, 0)), false)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateDate()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new DateTime(2017, 9, 2), true)
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateYearMonth()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new GYearMonth(2017, 7, "Z"))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateYearMonthNoTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new GYearMonth(2017, 7, null))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateYear()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new GYear(2017, "Z"))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateYearNoTimeZone()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new GYear(2017, null))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateDuration()
        {
            ItemContainer container = new ItemContainer();
            Animal animal = new Animal
            {
                ID = Guid.NewGuid().ToString(),
                CDate = new CogsDate(new TimeSpan(1562))
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void SimpleTypeCogsDateList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ReusableToItem()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ReusabletoSimple()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ReusabletoSimpleList()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void NestedReusableItem()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void NestedReusableItemLists()
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

            XmlValidation(container.MakeXml());
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
        public void ListOfReusableType()
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
                LingualDescription = new List<MultilingualString>() { describe1, describe2 }
            };
            container.Items.Add(animal);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleTypeGyear()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleLangString()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleTypeGMonth()
        {
            ItemContainer container = new ItemContainer();
            GMonth month1 = new GMonth(1, "Z");
            GMonth month2 = new GMonth(9, "+09:00");
            GMonth month3 = new GMonth(12, "+01:00");
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gmonth = new List<GMonth>() { month1, month2, month3 }
            };
            container.Items.Add(bread);

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleTypeGDay()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleTypeGMonthDay()
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

            XmlValidation(container.MakeXml());
        }

        [Fact]
        public void ListOfSimpleTypeGYearMonth()
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

            XmlValidation(container.MakeXml());
        }

        private void XmlValidation(XDocument doc)
        {
            XmlSchemaSet schemas = GetXmlSchema();

            List<ValidationEventArgs> errors = new List<ValidationEventArgs>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.Schemas = schemas;
            settings.ValidationEventHandler += (o, e) => 
            {
                Console.WriteLine("{0}", e.Message);
                errors.Add(e);
            };

            using (XmlReader xr = XmlReader.Create(doc.CreateReader(), settings))
            {
                while (xr.Read()) { }
            }
            Assert.Empty(errors);
        }

        private static void ValidationCallback(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Error) { Assert.True(false); }
            else if (args.Severity == XmlSeverityType.Warning) { Assert.True(false); }
        }

        private XmlSchemaSet GetXmlSchema()
        {
            // TODO build the json schema into the generated assembly as a resource
            string schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "generated", "schema.xsd");
            

            XmlSchemaSet xmlSchemaSet = new XmlSchemaSet();
            xmlSchemaSet.ValidationEventHandler += new ValidationEventHandler(ValidationCallback);
            
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;

            using (XmlReader reader = XmlReader.Create(schemaPath, settings))
            {
                XmlSchema xmlSchema = XmlSchema.Read(reader, new ValidationEventHandler(ValidationCallback));
                xmlSchemaSet.Add(xmlSchema);
            }

            xmlSchemaSet.Compile();

            return xmlSchemaSet;
        }
    }
}
