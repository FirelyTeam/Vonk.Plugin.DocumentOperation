using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Vonk.Core.Context.Features;
using Vonk.Core.Pluggability;
using Vonk.Core.Repository;

namespace VonkDocumentOperation
{
    [VonkConfiguration(order: 4900)]
    public static class DocumentOperationConfiguration
    {
        // Add services here to the DI system of ASP.NET Core
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<ISearchRepository, DocumentRepository>(); // Implements the interface for a FHIR READ operation
            return services;
        }

        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            //add middleware to the pipeline being built with the builder
            return builder;
        }
    }
}
