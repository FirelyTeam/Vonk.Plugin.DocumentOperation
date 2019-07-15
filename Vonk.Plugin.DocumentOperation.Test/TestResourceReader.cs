using System;
using System.IO;
using System.Reflection;

namespace Vonk.Test.Utils
{
    public static class TestResourceReader
    {
        private static string GetFullPathForExample(string filename)
        {
            string location = typeof(TestResourceReader).GetTypeInfo().Assembly.Location;
            var path = Path.GetDirectoryName(location);
            return Path.Combine(path, "TestData", filename);
        }

        public static string ReadTestData(string filename)
        {
            string file = GetFullPathForExample(filename);
            return File.ReadAllText(file);
        }
    }
}
