using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Publishers
{
    public class ModelInitializer
    {
        public string Dir { get; set; }

        public void Create()
        {
            if (Directory.Exists(Dir))
            {
                throw new InvalidDataException("The directory already exist");
            }
            //Create the directory that user want
            DirectoryInfo di = Directory.CreateDirectory(Dir);

            //Create the 4 major folders and 1 readme file
            DirectoryInfo it = Directory.CreateDirectory(Path.Combine(di.FullName, "ItemTypes"));
            DirectoryInfo rt = Directory.CreateDirectory(Path.Combine(di.FullName, "ReusableTypes"));
            DirectoryInfo setting = Directory.CreateDirectory(Path.Combine(di.FullName, "Settings"));
            DirectoryInfo topics = Directory.CreateDirectory(Path.Combine(di.FullName, "Topics"));
            File.WriteAllText(Path.Combine(di.FullName, "readme.md"), "Model description");

            //Create itemtype
            DirectoryInfo i1 = Directory.CreateDirectory(Path.Combine(it.FullName, "item1"));
            DirectoryInfo i2 = Directory.CreateDirectory(Path.Combine(it.FullName, "item2"));

            //write to the directory
            //create item1
            StringBuilder item1 = new StringBuilder();
            StringBuilder item1readme = new StringBuilder();
            item1.AppendLine("Name,DataType,MinCardinality,MaxCardinality,Description,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup");
            item1.AppendLine("name,string,1,1,name of the object being referenced.,,,,,,,,,,,");
            item1.AppendLine("ID,string,1,1,ID of the object being referenced.,,,,,,,,,,,");
            File.WriteAllText(Path.Combine(i1.FullName, "item1.csv"), item1.ToString());
            item1readme.Append("This is itemtype item1");
            File.WriteAllText(Path.Combine(i1.FullName, "readme.md"), item1readme.ToString());

            //create item2
            StringBuilder item2 = new StringBuilder();
            StringBuilder item2readme = new StringBuilder();
            item2.AppendLine("Name,DataType,MinCardinality,MaxCardinality,Description,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup");
            item2.AppendLine("name,string,1,1,name of the object being referenced.,,,,,,,,,,,");
            item2.AppendLine("ID,string,1,1,ID of the object being referenced.,,,,,,,,,,,");
            File.WriteAllText(Path.Combine(i2.FullName, "item2.csv"), item2.ToString());
            item2readme.Append("This is itemtype item2");
            File.WriteAllText(Path.Combine(i2.FullName, "readme.md"), item2readme.ToString());


            //Create reusabletype
            DirectoryInfo r1 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type1"));
            DirectoryInfo r2 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type2"));
            //write to direcotry
            //create reuse1
            StringBuilder reuse1 = new StringBuilder();
            StringBuilder reuse1readme = new StringBuilder();
            reuse1.AppendLine("Name,DataType,MinCardinality,MaxCardinality,Description,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup");
            reuse1.AppendLine("val,int,1,1,value of the object being referenced.,,,,,,,,,,,");
            File.WriteAllText(Path.Combine(r1.FullName, "Type1.csv"), reuse1.ToString());
            reuse1readme.Append("This is reusabletype reuse1");
            File.WriteAllText(Path.Combine(r1.FullName, "readme.md"), reuse1readme.ToString());

            //create reuse2
            StringBuilder reuse2 = new StringBuilder();
            StringBuilder reuse2readme = new StringBuilder();
            reuse2.AppendLine("Name,DataType,MinCardinality,MaxCardinality,Description,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup");
            reuse2.AppendLine("length,double,0,1,value of the object being referenced.,,,,,,,,,,,");
            File.WriteAllText(Path.Combine(r2.FullName, "Type2.csv"), reuse2.ToString());
            reuse2readme.Append("This is reusabletype reuse2");
            File.WriteAllText(Path.Combine(r2.FullName, "readme.md"), reuse2readme.ToString());

            //Create setting
            StringBuilder identification = new StringBuilder();
            identification.AppendLine("Name,DataType,MinCardinality,MaxCardinality,Description,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup");
            identification.AppendLine("ID,string,1,1,ID of the object being referenced.,,,,,,,,,,,");
            StringBuilder settinginfo = new StringBuilder();
            settinginfo.Append("this is the setting info csv file");
            File.WriteAllText(Path.Combine(setting.FullName, "identication.csv"), identification.ToString());
            File.WriteAllText(Path.Combine(setting.FullName, "Settings.csv"), settinginfo.ToString());

            //Create topics
            StringBuilder index = new StringBuilder();
            index.Append("All content items");
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
