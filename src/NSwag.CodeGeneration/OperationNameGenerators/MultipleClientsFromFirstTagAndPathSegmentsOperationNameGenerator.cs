//-----------------------------------------------------------------------
// <copyright file="MultipleClientsFromFirstTagAndPathSegmentsOperationNameGenerator.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/RicoSuter/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using NJsonSchema;
using System.Collections.Generic;
using System;
using System.Linq;

namespace NSwag.CodeGeneration.OperationNameGenerators
{
    /// <summary>Generates the client name based on the first tag and operation name based on the path segments (operation name = last segment, client name = first tag).</summary>
    public class MultipleClientsFromFirstTagAndPathSegmentsOperationNameGenerator : IOperationNameGenerator
    {
        /// <summary>Gets a value indicating whether the generator supports multiple client classes.</summary>
        public bool SupportsMultipleClients { get; } = true;

        /// <summary>Gets the client name for a given operation (may be empty).</summary>
        /// <param name="document">The Swagger document.</param>
        /// <param name="path">The HTTP path.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="operation">The operation.</param>
        /// <returns>The client name.</returns>
        public virtual string GetClientName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
        {
            return ConversionUtilities.ConvertToUpperCamelCase(operation.Tags.FirstOrDefault(), false);
        }

        /// <summary>Gets the operation name for a given operation.</summary>
        /// <param name="document">The Swagger document.</param>
        /// <param name="path">The HTTP path.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="operation">The operation.</param>
        /// <returns>The operation name.</returns>
        public virtual string GetOperationName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
        {
            var operationName = ConvertPathToName(path);
            bool isDuplicate = false;
            if (document.ClientSuffix == "v2")
            {
                isDuplicate = CheckForDuplicatePaths(path, document);
                if (isDuplicate)
                {
                    operationName += operationName + GetSecondToLastValue(path);
                }
            }
            if (document.ClientSuffix != "v2")
            {
                var hasNameConflict = document.Paths
                    .SelectMany(pair => pair.Value.Select(p => new { Path = pair.Key.Trim('/'), HttpMethod = p.Key, Operation = p.Value }))
                    .Where(op =>
                        GetClientName(document, op.Path, op.HttpMethod, op.Operation) == GetClientName(document, path, httpMethod, operation) &&
                        ConvertPathToName(op.Path) == operationName
                    ).ToList()
                    .Count > 1;

                if (hasNameConflict)
                {
                    operationName += ConversionUtilities.ConvertToUpperCamelCase(httpMethod, false);
                }
            }
            
            return operationName;
        }

        public string GetSecondToLastValue(string path)
        {
            // Split the path into segments
            var pathSegments = path.Split('/');

            // Iterate backwards to find the second-to-last segment that is not enclosed in {}
            for (int i = pathSegments.Length - 2; i >= 0; i--)
            {
                if (!pathSegments[i].StartsWith("{") && !pathSegments[i].EndsWith("}"))
                {
                    return pathSegments[i]; // Return the second-to-last value that is not a parameter
                }
            }

            return string.Empty; // Return empty string if no match found
        }

        public bool CheckForDuplicatePaths(string basePathWithParams, OpenApiDocument document)
        {
            bool isDuplicate = false; // Initialize the flag

            // Split the base path into segments (e.g., "v2/amla/{portal}/{locale}")
            var basePathSegments = basePathWithParams.Split('/');

            // Extract the base path (e.g., "v2/amla")
            string basePath = "/" + string.Join("/", basePathSegments.Take(2)); // Take only the first 2 segments

            // Extract the parameters (e.g., "{portal}", "{locale}")
            var basePathParams = basePathSegments.Skip(2).Where(s => s.StartsWith("{") && s.EndsWith("}")).ToList();

            // Iterate through the document paths
            foreach (var pathItem in document.Paths)
            {
                // Split the document path into segments
                var pathSegments = pathItem.Key.Split('/');

                // Check if the path starts with the base path
                if (pathItem.Key.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract parameters from the matched path (after the base path)
                    var matchedPathParams = pathSegments.Skip(2).Where(s => s.StartsWith("{") && s.EndsWith("}")).ToList();

                    // Compare the parameters from the provided base path with the matched path parameters
                    if (basePathParams.SequenceEqual(matchedPathParams, StringComparer.OrdinalIgnoreCase))
                    {
                        // Set the flag to true if both path and parameters match
                        isDuplicate = true;
                        break; // No need to check further if a duplicate is found
                    }
                }
            }

            return isDuplicate;
        }

        /// <summary>Converts the path to an operation name.</summary>
        /// <param name="path">The HTTP path.</param>
        /// <returns>The operation name.</returns>
        internal static string ConvertPathToName(string path)
        {
            return path
                .Split('/')
                .Where(p => !p.Contains("{") && !string.IsNullOrWhiteSpace(p))
                .Reverse()
                .FirstOrDefault() ?? "Index";
        }
    }
}