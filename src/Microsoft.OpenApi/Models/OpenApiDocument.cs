﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. 

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Writers;

namespace Microsoft.OpenApi.Models
{
    /// <summary>
    /// Describes an OpenAPI object (OpenAPI document). See: https://swagger.io/specification
    /// </summary>
    public class OpenApiDocument : IOpenApiSerializable, IOpenApiExtensible
    {
        /// <summary>
        /// REQUIRED. Provides metadata about the API. The metadata MAY be used by tooling as required.
        /// </summary>
        public OpenApiInfo Info { get; set; }

        /// <summary>
        /// An array of Server Objects, which provide connectivity information to a target server.
        /// </summary>
        public IList<OpenApiServer> Servers { get; set; } = new List<OpenApiServer>();

        /// <summary>
        /// REQUIRED. The available paths and operations for the API.
        /// </summary>
        public OpenApiPaths Paths { get; set; } = new OpenApiPaths();

        /// <summary>
        /// An element to hold various schemas for the specification.
        /// </summary>
        public OpenApiComponents Components { get; set; }

        /// <summary>
        /// A declaration of which security mechanisms can be used across the API.
        /// </summary>
        public IList<OpenApiSecurityRequirement> SecurityRequirements { get; set; } =
            new List<OpenApiSecurityRequirement>();

        /// <summary>
        /// A list of tags used by the specification with additional metadata.
        /// </summary>
        public IList<OpenApiTag> Tags { get; set; } = new List<OpenApiTag>();

        /// <summary>
        /// Additional external documentation.
        /// </summary>
        public OpenApiExternalDocs ExternalDocs { get; set; }

        /// <summary>
        /// This object MAY be extended with Specification Extensions.
        /// </summary>
        public IDictionary<string, IOpenApiAny> Extensions { get; set; } = new Dictionary<string, IOpenApiAny>();

        /// <summary>
        /// Serialize <see cref="OpenApiDocument"/> to the latest patch of OpenAPI object V3.0.
        /// </summary>
        public void SerializeAsV3(IOpenApiWriter writer)
        {
            if (writer == null)
            {
                throw Error.ArgumentNull(nameof(writer));
            }

            writer.WriteStartObject();

            // openapi
            writer.WriteProperty(OpenApiConstants.OpenApi, "3.0.1");

            // info
            writer.WriteRequiredObject(OpenApiConstants.Info, Info, (w, i) => i.SerializeAsV3(w));

            // servers
            writer.WriteOptionalCollection(OpenApiConstants.Servers, Servers, (w, s) => s.SerializeAsV3(w));

            // paths
            writer.WriteRequiredObject(OpenApiConstants.Paths, Paths, (w, p) => p.SerializeAsV3(w));

            // components
            writer.WriteOptionalObject(OpenApiConstants.Components, Components, (w, c) => c.SerializeAsV3(w));

            // security
            writer.WriteOptionalCollection(
                OpenApiConstants.Security,
                SecurityRequirements,
                (w, s) => s.SerializeAsV3(w));

            // tags
            writer.WriteOptionalCollection(OpenApiConstants.Tags, Tags, (w, t) => t.SerializeAsV3WithoutReference(w));

            // external docs
            writer.WriteOptionalObject(OpenApiConstants.ExternalDocs, ExternalDocs, (w, e) => e.SerializeAsV3(w));

            // extensions
            writer.WriteExtensions(Extensions);

            writer.WriteEndObject();
        }

        /// <summary>
        /// Serialize <see cref="OpenApiDocument"/> to OpenAPI object V2.0.
        /// </summary>
        public void SerializeAsV2(IOpenApiWriter writer)
        {
            if (writer == null)
            {
                throw Error.ArgumentNull(nameof(writer));
            }

            writer.WriteStartObject();

            // swagger
            writer.WriteProperty(OpenApiConstants.Swagger, "2.0");

            // info
            writer.WriteRequiredObject(OpenApiConstants.Info, Info, (w, i) => i.SerializeAsV2(w));

            // host, basePath, schemes, consumes, produces
            WriteHostInfoV2(writer, Servers);

            // paths
            writer.WriteRequiredObject(OpenApiConstants.Paths, Paths, (w, p) => p.SerializeAsV2(w));

            // Serialize each referenceable object as full object without reference if the reference in the object points to itself. 
            // If the reference exists but points to other objects, the object is serialized to just that reference.

            // definitions
            writer.WriteOptionalMap(
                OpenApiConstants.Definitions,
                Components?.Schemas,
                (w, key, component) =>
                {
                    if (component.Reference != null &&
                        component.Reference.Type == ReferenceType.Schema &&
                        component.Reference.Id == key)
                    {
                        component.SerializeAsV2WithoutReference(w);
                    }
                    else
                    {
                        component.SerializeAsV2(w);
                    }
                });

            // parameters
            writer.WriteOptionalMap(
                OpenApiConstants.Parameters,
                Components?.Parameters,
                (w, key, component) =>
                {
                    if (component.Reference != null &&
                        component.Reference.Type == ReferenceType.Parameter &&
                        component.Reference.Id == key)
                    {
                        component.SerializeAsV2WithoutReference(w);
                    }
                    else
                    {
                        component.SerializeAsV2(w);
                    }
                });

            // responses
            writer.WriteOptionalMap(
                OpenApiConstants.Responses,
                Components?.Responses,
                (w, key, component) =>
                {
                    if (component.Reference != null &&
                        component.Reference.Type == ReferenceType.Response &&
                        component.Reference.Id == key)
                    {
                        component.SerializeAsV2WithoutReference(w);
                    }
                    else
                    {
                        component.SerializeAsV2(w);
                    }
                });

            // securityDefinitions
            writer.WriteOptionalMap(
                OpenApiConstants.SecurityDefinitions,
                Components?.SecuritySchemes,
                (w, key, component) =>
                {
                    if (component.Reference != null &&
                        component.Reference.Type == ReferenceType.SecurityScheme &&
                        component.Reference.Id == key)
                    {
                        component.SerializeAsV2WithoutReference(w);
                    }
                    else
                    {
                        component.SerializeAsV2(w);
                    }
                });

            // security
            writer.WriteOptionalCollection(
                OpenApiConstants.Security,
                SecurityRequirements,
                (w, s) => s.SerializeAsV2(w));

            // tags
            writer.WriteOptionalCollection(OpenApiConstants.Tags, Tags, (w, t) => t.SerializeAsV2WithoutReference(w));

            // externalDocs
            writer.WriteOptionalObject(OpenApiConstants.ExternalDocs, ExternalDocs, (w, e) => e.SerializeAsV2(w));

            // extensions
            writer.WriteExtensions(Extensions);

            writer.WriteEndObject();
        }

        private static void WriteHostInfoV2(IOpenApiWriter writer, IList<OpenApiServer> servers)
        {
            if (servers == null || !servers.Any())
            {
                return;
            }

            // Arbitrarily choose the first server given that V2 only allows 
            // one host, port, and base path.
            var firstServer = servers.First();

            // Divide the URL in the Url property into host and basePath required in OpenAPI V2
            // The Url property cannotcontain path templating to be valid for V2 serialization.
            var firstServerUrl = new Uri(firstServer.Url);

            // host
            writer.WriteProperty(
                OpenApiConstants.Host,
                firstServerUrl.GetComponents(UriComponents.Host | UriComponents.Port, UriFormat.SafeUnescaped));

            // basePath
            writer.WriteProperty(OpenApiConstants.BasePath, firstServerUrl.AbsolutePath);

            // Consider all schemes of the URLs in the server list that have the same
            // host, port, and base path as the first server.
            var schemes = servers.Select(
                    s =>
                    {
                        Uri.TryCreate(s.Url, UriKind.RelativeOrAbsolute, out var url);
                        return url;
                    })
                .Where(
                    u => Uri.Compare(
                            u,
                            firstServerUrl,
                            UriComponents.Host | UriComponents.Port | UriComponents.Path,
                            UriFormat.SafeUnescaped,
                            StringComparison.OrdinalIgnoreCase) ==
                        0)
                .Select(u => u.Scheme)
                .Distinct()
                .ToList();

            // schemes
            writer.WriteOptionalCollection(OpenApiConstants.Schemes, schemes, (w, s) => w.WriteValue(s));
        }
    }
}