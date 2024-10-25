//-----------------------------------------------------------------------
// <copyright file="ClientGeneratorBase.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/RicoSuter/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NSwag.CodeGeneration.Models;

namespace NSwag.CodeGeneration
{
    /// <summary>The client generator base.</summary>
    /// <typeparam name="TOperationModel">The type of the operation model.</typeparam>
    /// <typeparam name="TParameterModel">The type of the parameter model.</typeparam>
    /// <typeparam name="TResponseModel">The type of the response model.</typeparam>
    /// <seealso cref="IClientGenerator" />
    public abstract class ClientGeneratorBase<TOperationModel, TParameterModel, TResponseModel> : IClientGenerator
        where TOperationModel : OperationModelBase<TParameterModel, TResponseModel>
        where TResponseModel : ResponseModelBase
        where TParameterModel : ParameterModelBase
    {
        private readonly OpenApiDocument _document;

        /// <summary>Initializes a new instance of the <see cref="ClientGeneratorBase{TOperationModel, TParameterModel, TResponseModel}"/> class.</summary>
        /// <param name="document">The document.</param>
        /// <param name="settings">The code generator settings.</param>
        /// <param name="resolver">The type resolver.</param>
        protected ClientGeneratorBase(OpenApiDocument document, CodeGeneratorSettingsBase settings, TypeResolverBase resolver)
        {
            _document = document;
            Resolver = resolver;

            settings.SchemaType = document.SchemaType; // enforce Swagger schema output
        }

        /// <summary>Gets the base settings.</summary>
        public abstract ClientGeneratorBaseSettings BaseSettings { get; }

        /// <summary>Gets the type resolver.</summary>
        protected TypeResolverBase Resolver { get; }

        /// <summary>Gets the type.</summary>
        /// <param name="schema">The schema.</param>
        /// <param name="isNullable">Specifies whether the type is nullable..</param>
        /// <param name="typeNameHint">The type name hint.</param>
        /// <returns>The type name.</returns>
        public abstract string GetTypeName(JsonSchema schema, bool isNullable, string typeNameHint);

        /// <summary>Gets the file response type name.</summary>
        /// <returns>The type name.</returns>
        public virtual string GetBinaryResponseTypeName()
        {
            return "FileResponse";
        }

        /// <summary>Generates the the whole file containing all needed types.</summary>
        /// <returns>The code</returns>
        public string GenerateFile()
        {
            return GenerateFile(ClientGeneratorOutputType.Full);
        }

        /// <summary>Generates the the whole file containing all needed types.</summary>
        /// <param name="outputType">Type of the output.</param>
        /// <returns>The code</returns>
        public string GenerateFile(ClientGeneratorOutputType outputType)
        {
            bool isInterfaceGeneration = false;
            bool isClientGeneration = _document.OutPutFilePathTypeScript.Split('\\').Last().ToLower().Contains("client");

            if (_document.OutPutFilePathTypeScript.TrimEnd('\\').ToLower().Contains("interface") || _document.OutPutFilePathTypeScript.TrimEnd('\\').ToLower().Contains("model")) 
                isInterfaceGeneration = true;

            var clientTypes = GenerateAllClientTypes();

            var dtoTypes = BaseSettings.GenerateDtoTypes ?
                GenerateDtoTypes(isClientGeneration) :
                Enumerable.Empty<CodeArtifact>();

            bool isTypeScript = dtoTypes.FirstOrDefault(x => x.Language == CodeArtifactLanguage.TypeScript) != null ||
                dtoTypes.FirstOrDefault(x => x.Language == CodeArtifactLanguage.CSharp) != null;

            var clientClassTypes =
                outputType == ClientGeneratorOutputType.Full ? clientTypes :
                outputType == ClientGeneratorOutputType.Implementation ? clientTypes.Where(t => t.Category != CodeArtifactCategory.Contract) :
                outputType == ClientGeneratorOutputType.Contracts ? clientTypes.Where(t => t.Category == CodeArtifactCategory.Contract) :
                Enumerable.Empty<CodeArtifact>();
            
                var dtoClassTypes = !isTypeScript ? (
                    outputType == ClientGeneratorOutputType.Full ||
                    outputType == ClientGeneratorOutputType.Contracts ? dtoTypes : Enumerable.Empty<CodeArtifact>()) : Enumerable.Empty<CodeArtifact>();

            if (isTypeScript && isInterfaceGeneration)
            {
                var clientInterfaceType =
                Enumerable.Empty<CodeArtifact>();

                var dtoInterfaceType = (
                    outputType == ClientGeneratorOutputType.Full ||
                    outputType == ClientGeneratorOutputType.Contracts ? dtoTypes : Enumerable.Empty<CodeArtifact>());

                return GenerateFile(clientInterfaceType, dtoInterfaceType, outputType)
                .Replace("\r", string.Empty)
                .Replace("\n\n\n\n", "\n\n")
                .Replace("\n\n\n", "\n\n");
            }

            return GenerateFile(clientClassTypes, dtoClassTypes, outputType)
                .Replace("\r", string.Empty)
                .Replace("\n\n\n\n", "\n\n")
                .Replace("\n\n\n", "\n\n");
        }

        /// <summary>Generates the file.</summary>
        /// <param name="clientTypes">The client types.</param>
        /// <param name="dtoTypes">The DTO types.</param>
        /// <param name="outputType">Type of the output.</param>
        /// <returns>The code.</returns>
        protected abstract string GenerateFile(IEnumerable<CodeArtifact> clientTypes, IEnumerable<CodeArtifact> dtoTypes, ClientGeneratorOutputType outputType);

        /// <summary>Generates the client types.</summary>
        /// <returns>The code artifact collection.</returns>
        protected virtual IEnumerable<CodeArtifact> GenerateAllClientTypes()
        {
            var operations = GetOperations(_document);
            var clientTypes = new List<CodeArtifact>();

            if (BaseSettings.OperationNameGenerator.SupportsMultipleClients)
            {
                var controllerOperationsGroups = operations.GroupBy(o => o.ControllerName);
                foreach (var controllerOperations in controllerOperationsGroups)
                {
                    var controllerName = controllerOperations.Key;
                    var controllerClassName = BaseSettings.GenerateControllerName(controllerOperations.Key);
                    var clientType = GenerateClientTypes(controllerName, controllerClassName, controllerOperations.ToList());
                    clientTypes.AddRange(clientType);
                }
            }
            else
            {
                var controllerName = string.Empty;
                var controllerClassName = BaseSettings.GenerateControllerName(controllerName);
                var clientType = GenerateClientTypes(controllerName, controllerClassName, operations);
                clientTypes.AddRange(clientType);
            }

            return clientTypes;
        }

        /// <summary>Generates the client class.</summary>
        /// <param name="controllerName">Name of the controller.</param>
        /// <param name="controllerClassName">Name of the controller class.</param>
        /// <param name="operations">The operations.</param>
        /// <returns>The code.</returns>
        protected abstract IEnumerable<CodeArtifact> GenerateClientTypes(string controllerName, string controllerClassName, IEnumerable<TOperationModel> operations);

        /// <summary>Generates all DTO types.</summary>
        /// <returns>The code artifact collection.</returns>
        protected abstract IEnumerable<CodeArtifact> GenerateDtoTypes(bool isClientGeneration = false);

        /// <summary>Creates an operation model.</summary>
        /// <param name="operation">The operation.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The operation model.</returns>
        protected abstract TOperationModel CreateOperationModel(OpenApiOperation operation, ClientGeneratorBaseSettings settings);

        private List<TOperationModel> GetOperations(OpenApiDocument document)
        {
            document.GenerateOperationIds();

            var result = new List<TOperationModel>();
            foreach (var pair in document.Paths)
            {
                foreach (var p in pair.Value.ActualPathItem)
                {
                    var path = pair.Key.TrimStart('/');
                    var httpMethod = p.Key;
                    var operation = p.Value;

                    var operationName = BaseSettings.OperationNameGenerator.GetOperationName(document, path, httpMethod, operation);

                    if (operationName.Contains("."))
                    {
                        operationName = operationName.Replace(".", "_");
                    }

                    if (operationName.EndsWith("Async"))
                    {
                        operationName = operationName.Substring(0, operationName.Length - "Async".Length);
                    }

                    var operationModel = CreateOperationModel(operation, BaseSettings);
                    operationModel.ControllerName = BaseSettings.OperationNameGenerator.GetClientName(document, path, httpMethod, operation);

                    operationModel.Path = path;

                    operationModel.HttpMethod = httpMethod;
                    operationModel.OperationName = operationName;
                    operationModel.ClientSuffix = document.ClientSuffix;
                    // Adding TagName logic
                    List<string> tagList = document?.TagName?.Split(',')?.ToList();

                    if (string.IsNullOrEmpty(document?.TagName))
                    {
                        result.Add(operationModel);
                    }
                    else
                    {
                        if (operation.Tags.Any(element => tagList.Any(item => item.Contains(element))))
                            result.Add(operationModel);
                    }
                }
            }
            return result;
        }
    }
}
