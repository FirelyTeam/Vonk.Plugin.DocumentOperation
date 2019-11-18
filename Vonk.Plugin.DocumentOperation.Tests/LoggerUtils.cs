using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace Vonk.UnitTests.Framework.Helpers
{
    public static class LoggerUtils
    {

        /// <summary>
        /// Returns an <pre>ILogger<T></pre> as used by the Microsoft.Logging framework.
        /// You can use this for constructors that require an ILogger parameter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ILogger<T> Logger<T>() where T : class
        {
            return LoggerMock<T>().Object;
        }

        public static Mock<ILogger<T>> LoggerMock<T>() where T : class
        {
            return new Mock<ILogger<T>>();
        }

    }
}
