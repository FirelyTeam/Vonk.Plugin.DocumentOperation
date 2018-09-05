using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Vonk.Core.Pluggability;
namespace VonkDocumentOperation
{
    [VonkConfiguration(order: 4900)]
    public static class DocumentOperationConfiguration
    {
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            //add services here to the DI system of ASP.NET Core
            return services;
        }

        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            //add middleware to the pipeline being built with the builder
            return builder;
        }
    }
}
