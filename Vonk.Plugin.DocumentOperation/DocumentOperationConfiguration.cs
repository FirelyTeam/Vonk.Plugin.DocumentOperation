using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vonk.Core.Pluggability;
using Vonk.Core.Context;

namespace Vonk.Plugin.DocumentOperation
{
    [VonkConfiguration(order: 4900)]
    public static class DocumentOperationConfiguration
    {
        // Add services here to the DI system of ASP.NET Core
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.TryAddScoped<DocumentService>(); // $document implementation
            return services;
        }

        // Add middleware to the pipeline being built with the builder
        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            // Register interactions
            builder.UseVonkInteractionAsync<DocumentService>((svc, context) => svc.DocumentInstanceGET(context), OperationType.Handler);

            return builder;
        }
    }
}
