﻿// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using NJsonSchema;
using NSwag.Generation.Processors.Contexts;
using NSwag.Generation.Processors;
using System.Net;
using System;
using System.Linq;

namespace Microsoft.AspNetCore.Datasync.NSwag
{
    /// <summary>
    /// Implements an <see cref="IOperationProcessor"/> for handling datasync table controllers.
    /// </summary>
    public class DatasyncOperationProcessor : IOperationProcessor
    {
        private static readonly Type tableControllerType = typeof(TableController<>);

        public bool Process(OperationProcessorContext context)
        {
            var baseType = context.ControllerType.BaseType;
            if (baseType?.IsGenericType == true && baseType?.GetGenericTypeDefinition() == tableControllerType)
            {
                ProcessDatasyncOperation(context);
                return true;
            }
            return true;
        }

        private static void ProcessDatasyncOperation(OperationProcessorContext context)
        {
            var operation = context.OperationDescription.Operation;
            var method = context.OperationDescription.Method;
            var path = context.OperationDescription.Path;
            Type entityType = context.ControllerType.BaseType?.GetGenericArguments().FirstOrDefault()
                ?? throw new ArgumentException("Cannot process a non-generic table controller");
            var entitySchema = context.SchemaResolver.GetSchema(entityType, false);
            var entitySchemaRef = new JsonSchema { Reference = entitySchema };

            operation.AddDatasyncRequestHeaders();
            if (method.Equals("DELETE", StringComparison.InvariantCultureIgnoreCase))
            {
                operation.AddConditionalRequestSupport(entitySchemaRef);
                operation.SetResponse(HttpStatusCode.NoContent);
                operation.SetResponse(HttpStatusCode.NotFound);
                operation.SetResponse(HttpStatusCode.Gone);
            }

            if (method.Equals("GET", StringComparison.InvariantCultureIgnoreCase) && path.EndsWith("/{id}"))
            {
                operation.AddConditionalRequestSupport(entitySchemaRef, true);
                operation.SetResponse(HttpStatusCode.OK, entitySchemaRef);
                operation.SetResponse(HttpStatusCode.NotFound);
            }

            if (method.Equals("GET", StringComparison.InvariantCultureIgnoreCase) && !path.EndsWith("/{id}"))
            {
                operation.AddODataQueryParameters();
                operation.SetResponse(HttpStatusCode.OK, CreateListSchema(entitySchemaRef, entityType.Name), false);
                operation.SetResponse(HttpStatusCode.BadRequest);
            }

            if (method.Equals("PATCH", StringComparison.InvariantCultureIgnoreCase))
            {
                operation.AddConditionalRequestSupport(entitySchemaRef);
                operation.SetResponse(HttpStatusCode.OK, entitySchemaRef);
                operation.SetResponse(HttpStatusCode.BadRequest);
                operation.SetResponse(HttpStatusCode.NotFound);
                operation.SetResponse(HttpStatusCode.Gone);
            }

            if (method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
            {
                operation.AddConditionalRequestSupport(entitySchemaRef, true);
                operation.SetResponse(HttpStatusCode.Created, entitySchemaRef);
                operation.SetResponse(HttpStatusCode.BadRequest);
            }

            if (method.Equals("PUT", StringComparison.InvariantCultureIgnoreCase))
            {
                operation.AddConditionalRequestSupport(entitySchemaRef);
                operation.SetResponse(HttpStatusCode.OK, entitySchemaRef);
                operation.SetResponse(HttpStatusCode.BadRequest);
                operation.SetResponse(HttpStatusCode.NotFound);
                operation.SetResponse(HttpStatusCode.Gone);
            }
        }

        /// <summary>
        /// Creates the paged item schema reference.
        /// </summary>
        /// <param name="entitySchema">The entity schema reference.</param>
        /// <returns>The list schema reference</returns>
        private static JsonSchema CreateListSchema(JsonSchema entitySchema, string entityName)
        {
            var listSchemaRef = new JsonSchema
            {
                Description = $"A page of {entityName} entities",
                Type = JsonObjectType.Object
            };
            listSchemaRef.Properties["items"] = new JsonSchemaProperty
            {
                Description = "The entities in this page of results",
                Type = JsonObjectType.Array,
                Item = entitySchema,
                IsReadOnly = true,
                IsNullableRaw = true
            };
            listSchemaRef.Properties["count"] = new JsonSchemaProperty
            {
                Description = "The count of all entities in the result set",
                Type = JsonObjectType.Integer,
                IsReadOnly = true,
                IsNullableRaw = true
            };
            listSchemaRef.Properties["nextLink"] = new JsonSchemaProperty
            {
                Description = "The URI to the next page of entities",
                Type = JsonObjectType.String,
                Format = "uri",
                IsReadOnly = true,
                IsNullableRaw = true
            };
            return listSchemaRef;
        }
    }
}
