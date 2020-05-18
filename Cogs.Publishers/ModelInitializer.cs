using Cogs.Dto;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cogs.Publishers
{
    public class ModelInitializer
    {
        public string Dir { get; set; }
        public bool Overwrite { get; set; }

        public void Create()
        {
            if (Directory.Exists(Dir))
            {
                if (Overwrite)
                {
                    Directory.Delete(Dir, true);
                }
                else { throw new InvalidDataException("The directory already exists"); }
            }

            //Create the directory that user want
            DirectoryInfo di = Directory.CreateDirectory(Dir);

            //Create the 4 major folders and 1 readme file
            DirectoryInfo it = Directory.CreateDirectory(Path.Combine(di.FullName, "ItemTypes"));
            DirectoryInfo rt = Directory.CreateDirectory(Path.Combine(di.FullName, "CompositeTypes"));
            DirectoryInfo setting = Directory.CreateDirectory(Path.Combine(di.FullName, "Settings"));
            DirectoryInfo topics = Directory.CreateDirectory(Path.Combine(di.FullName, "Topics"));
            File.WriteAllText(Path.Combine(di.FullName, "readme.md"), "Model description");

            //Create itemtype
            DirectoryInfo i1 = Directory.CreateDirectory(Path.Combine(it.FullName, "item1"));
            DirectoryInfo i2 = Directory.CreateDirectory(Path.Combine(it.FullName, "item2"));

            //write to the directory
            //create items
            List<Property> items = new List<Property>()
            {
                new Property()
                {
                    Name="name",
                    DataType="string",
                    MinCardinality="1",
                    MaxCardinality="1",
                    Description="Name of the object being referenced"
                },
                new Property()
                {
                    Name="ID",
                    DataType="string",
                    MinCardinality="1",
                    MaxCardinality="1",
                    Description="ID of the object being referenced"
                }
            };
            TextWriter textwriter = null;
            CsvWriter csv = null;
            using (textwriter = new StringWriter())
            {
                csv = new CsvWriter(textwriter, CultureInfo.InvariantCulture);

                csv.WriteRecords(items);
                File.WriteAllText(Path.Combine(i1.FullName, "item1.csv"), textwriter.ToString());
                File.WriteAllText(Path.Combine(i2.FullName, "item2.csv"), textwriter.ToString());
            }
            StringBuilder item1readme = new StringBuilder();
            item1readme.Append("This is itemtype item1");
            File.WriteAllText(Path.Combine(i1.FullName, "readme.md"), item1readme.ToString());

            StringBuilder item2readme = new StringBuilder();
            item2readme.Append("This is itemtype item2");
            File.WriteAllText(Path.Combine(i2.FullName, "readme.md"), item2readme.ToString());


            //Create reusabletype
            DirectoryInfo r1 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type1"));
            DirectoryInfo r2 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type2"));

            List<Property>reusableitem1 = new List<Property>()
            {
                new Property()
                {
                    Name="val",
                    DataType="int",
                    MinCardinality="1",
                    MaxCardinality="1",
                    Description="value of the object being referenced"
                }
            };
            textwriter = null;
            csv = null;
            using (textwriter = new StringWriter())
            {
                csv = new CsvWriter(textwriter, CultureInfo.InvariantCulture);
                csv.WriteRecords(reusableitem1);
                File.WriteAllText(Path.Combine(r1.FullName, "Type1.csv"), textwriter.ToString());
            }
            StringBuilder reuse1readme = new StringBuilder();
            reuse1readme.Append("This is reusabletype reuse1");
            File.WriteAllText(Path.Combine(r1.FullName, "readme.md"), reuse1readme.ToString());

            //create reuse2
            List<Property> reusableitem2 = new List<Property>()
            {
                new Property()
                {
                    Name="length",
                    DataType="double",
                    MinCardinality="1",
                    MaxCardinality="1",
                    Description="length of the object being referenced"
                }
            };
            textwriter = null;
            csv = null;
            using (textwriter = new StringWriter())
            {
                csv = new CsvWriter(textwriter, CultureInfo.InvariantCulture);
                csv.WriteRecords(reusableitem2);
                File.WriteAllText(Path.Combine(r2.FullName, "Type2.csv"), textwriter.ToString());
            }
            StringBuilder reuse2readme = new StringBuilder();
            reuse2readme.Append("This is reusabletype reuse2");
            File.WriteAllText(Path.Combine(r2.FullName, "readme.md"), reuse2readme.ToString());

            //Create setting
            List<Property> identification1 = new List<Property>()
            {
                new Property()
                {
                    Name="ID",
                    DataType="string",
                    MinCardinality="1",
                    MaxCardinality="1",
                    Description="ID of the object being referenced"
                }
            };
            textwriter = null;
            csv = null;
            using (textwriter = new StringWriter())
            {
                csv = new CsvWriter(textwriter, CultureInfo.InvariantCulture);
                csv.WriteRecords(identification1);
                File.WriteAllText(Path.Combine(setting.FullName, "Identification.csv"), textwriter.ToString());
            }
            StringBuilder settinginfo = new StringBuilder();
            settinginfo.AppendLine(@"""Key"",""Value""");
            settinginfo.AppendLine(@"""Title"",""My Model""");
            settinginfo.AppendLine(@"""ShortTitle"",""MyModel""");
            settinginfo.AppendLine(@"""Slug"",""mymodel""");
            settinginfo.AppendLine(@"""Description"",""A description for my model""");
            settinginfo.AppendLine(@"""Version"",""0.1""");
            settinginfo.AppendLine(@"""Author"",""Me""");
            settinginfo.AppendLine(@"""Copyright"",""Copyright (c) 2017 Authors""");
            settinginfo.AppendLine(@"""NamespaceUrl"",""http://example.org/mymodel""");
            settinginfo.AppendLine(@"""NamespacePrefix"",""mymodel""");

            File.WriteAllText(Path.Combine(setting.FullName, "Settings.csv"), settinginfo.ToString());

            //Create topics
            StringBuilder index = new StringBuilder();
            index.Append("All");
            DirectoryInfo All = Directory.CreateDirectory(Path.Combine(topics.FullName, "All"));
            File.WriteAllText(Path.Combine(topics.FullName, "index.txt"), index.ToString());

            StringBuilder index_item = new StringBuilder();
            index_item.Append("item1 item2");
            StringBuilder readme = new StringBuilder();
            readme.Append("simple readme file");
            File.WriteAllText(Path.Combine(All.FullName, "items.txt"), index_item.ToString());
            File.WriteAllText(Path.Combine(All.FullName, "readme.md"), readme.ToString());
        }   
    }
}
