using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using NJsonSchema;
using System;
using System.IO;

namespace Cogs.Tests.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var task = AsyncJsonTest.MainAsync();
            try
            {
                task.Wait();
                System.Console.WriteLine("finished");
                System.Console.ReadKey();
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.ToString());
                System.Console.ReadKey();
            }
        }

    }
}
