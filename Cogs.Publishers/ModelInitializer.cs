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

            //Create reusabletype
            DirectoryInfo r1 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type1"));
            DirectoryInfo r2 = Directory.CreateDirectory(Path.Combine(rt.FullName, "Type2"));

            //Create setting
            StringBuilder identification = new StringBuilder();
            identification.Append("this is a identification csv file");
            StringBuilder settinginfo = new StringBuilder();
            settinginfo.Append("this is the setting info csv file");
            File.WriteAllText(Path.Combine(setting.FullName, "identication.csv"), identification.ToString());
            File.WriteAllText(Path.Combine(setting.FullName, "Settings.csv"), settinginfo.ToString());

            //Create topics
            StringBuilder index = new StringBuilder();
            index.Append("this is a index.txt file");
            DirectoryInfo All = Directory.CreateDirectory(Path.Combine(topics.FullName, "All"));
            File.WriteAllText(Path.Combine(topics.FullName, "index.txt"), index.ToString());

            StringBuilder index_item = new StringBuilder();
            index_item.Append("a list of index item");
            StringBuilder readme = new StringBuilder();
            readme.Append("simple readme file");
            File.WriteAllText(Path.Combine(All.FullName, "items.txt"), index_item.ToString());
            File.WriteAllText(Path.Combine(All.FullName, "readme.md"), readme.ToString());
        }   
    }
}
