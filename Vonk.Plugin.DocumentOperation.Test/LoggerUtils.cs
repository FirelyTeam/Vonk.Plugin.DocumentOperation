using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace Vonk.Test.Utils
{
    public class LoggerUtils
    {
        public static Mock<ILogger<T>> LoggerMock<T>() where T : class
        {
            return new Mock<ILogger<T>>();
        }

        public static ILogger<T> Logger<T>() where T : class
        {
            return LoggerMock<T>().Object;
        }

    }
}
