using System;
using System.Linq.Expressions;
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
            services.AddScoped<DocumentRepository>(); // $document implementation
            return services;
        }

        // Add middleware to the pipeline being built with the builder
        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            builder.UseVonkInteraction<DocumentRepository>((svc, context) => svc.document(context)); // Register interaction
            return builder;
        }
    }
}
