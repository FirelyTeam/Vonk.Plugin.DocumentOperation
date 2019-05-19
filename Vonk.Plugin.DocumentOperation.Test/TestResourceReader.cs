using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Vonk.Test.Utils
{
    public class TestResourceReader
    {
        private Assembly _resourcesAssembly;
        private Func<string, string> _getResourceName;

        public TestResourceReader(Type assemblyContainingType = null)
        {
            if (assemblyContainingType == null)
                assemblyContainingType = this.GetType();

            _resourcesAssembly = assemblyContainingType.GetTypeInfo().Assembly;
            var assemblyName = _resourcesAssembly.GetName().Name;

            _getResourceName = (filename) =>
            {
                //Make it work for minimally and maximally specified filenames.
                if (!Path.HasExtension(filename))
                    filename = $"{filename}.json";
                if (!filename.StartsWith($"{assemblyName}.Resources."))
                    filename = $"{assemblyName}.Resources.{filename}";
                return filename;
            };
        }

        public string GetResourceAsString(string filename)
        {
            using (var sr = new StreamReader(GetResourceAsStream(filename)))
            {
                return sr.ReadToEnd();
            }
        }
        public IEnumerable<string> GetAllResourcesAsString(string extension = null)
        {
            foreach (var resourceName in _resourcesAssembly.GetManifestResourceNames())
            {
                if (String.IsNullOrEmpty(extension) || resourceName.EndsWith(extension))
                    yield return GetResourceAsString(resourceName);
            }
        }

        /// <summary>
        /// Be sure to dispose of the stream yourself!
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public Stream GetResourceAsStream(string filename)
        {
            filename = _getResourceName(filename);
            if (!_resourcesAssembly.GetManifestResourceNames().Contains(filename))
                throw new ArgumentException($"No such file in the resources: {filename}");
            return _resourcesAssembly.GetManifestResourceStream(_getResourceName(filename));
        }
    }
}
