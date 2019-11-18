using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vonk.Core.Context;
using Vonk.Core.Metadata;
using Vonk.Core.Pluggability;
using Vonk.Core.Support;

namespace Vonk.Plugin.DocumentOperation
{
    [VonkConfiguration(order: 4900)]
    public static class DocumentOperationConfiguration
    {
        // Add services here to the DI system of ASP.NET Core
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.TryAddScoped<DocumentService>(); // $document implementation
            services.AddIfNotExists
                <ICapabilityStatementContributor, DocumentOperationConformanceContributor>
                (ServiceLifetime.Transient);
            return services;
        }

        // Add middleware to the pipeline being built with the builder
        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            // Register interactions
            builder
                .OnCustomInteraction(VonkInteraction.instance_custom, "document")
                .AndResourceTypes(new[] { "Composition" })
                .AndMethod("GET")
                .HandleAsyncWith<DocumentService>((svc, context)
                    => svc.DocumentInstanceGET(context));

            builder
                .OnCustomInteraction(VonkInteraction.type_custom, "document")
                .AndResourceTypes(new[] { "Composition" })
                .AndMethod("POST")
                .HandleAsyncWith<DocumentService>((svc, context)
                    => svc.DocumentTypePOST(context));

            return builder;
        }
    }
}
