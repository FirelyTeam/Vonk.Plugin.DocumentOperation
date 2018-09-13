using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Vonk.Core.Pluggability;

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
            // Register interactions
            builder.UseVonkInteraction<DocumentRepository>((doc, context) => doc.DocumentTypeGET(context), OperationType.Handler);
            builder.UseVonkInteraction<DocumentRepository>((doc, context) => doc.DocumentTypePOST(context), OperationType.Handler);
            builder.UseVonkInteraction<DocumentRepository>((doc, context) => doc.DocumentInstanceGET(context), OperationType.Handler);
            builder.UseVonkInteraction<DocumentRepository>((doc, context) => doc.DocumentInstancePOST(context), OperationType.Handler);
            return builder;
        }
    }
}
