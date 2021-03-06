﻿//-----------------------------------------------------------------------
// <copyright file="IJsonNavigator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Generic;

    /// <summary>
    /// JsonNavigator interface for classes that can navigate jsons.
    /// </summary>
    internal interface IJsonNavigator
    {
        /// <summary>
        /// Gets the <see cref="JsonSerializationFormat"/> for the IJsonNavigator.
        /// </summary>
        JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Gets <see cref="IJsonNavigatorNode"/> of the root node.
        /// </summary>
        /// <returns><see cref="IJsonNavigatorNode"/> corresponding to the root node.</returns>
        IJsonNavigatorNode GetRootNode();

        /// <summary>
        /// Gets the <see cref="JsonNodeType"/> type for a particular node
        /// </summary>
        /// <param name="node">The <see cref="IJsonNavigatorNode"/> of the node you want to know the type of</param>
        /// <returns><see cref="JsonNodeType"/> for the node</returns>
        JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <summary>
        /// Gets the numeric value for a node
        /// </summary>
        /// <param name="numberNode">The <see cref="IJsonNavigatorNode"/> of the node you want the number value from.</param>
        /// <returns>A double that represents the number value in the node.</returns>
        double GetNumberValue(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Tries to get the buffered string value from a node.
        /// </summary>
        /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the buffered string from.</param>
        /// <param name="bufferedStringValue">The buffered string value if possible</param>
        /// <returns><code>true</code> if the JsonNavigator successfully got the buffered string value; <code>false</code> if the JsonNavigator failed to get the buffered string value.</returns>
        bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out IReadOnlyList<byte> bufferedStringValue);

        /// <summary>
        /// Gets a string value from a node.
        /// </summary>
        /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the string value from.</param>
        /// <returns>The string value from the node.</returns>
        string GetStringValue(IJsonNavigatorNode stringNode);

        /// <summary>
        /// Gets the number of elements in an array node.
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to get the count of.</param>
        /// <returns>The number of elements in the array node.</returns>
        int GetArrayItemCount(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the node at a particular index of an array node
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to index from.</param>
        /// <param name="index">The offset into the array</param>
        /// <returns>The <see cref="IJsonNavigatorNode"/> of the node at a particular index of an array node</returns>
        IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index);

        /// <summary>
        /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the array to get the items from</param>
        /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.</returns>
        IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the number of properties in an object node.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of node to get the property count from.</param>
        /// <returns>The number of properties in an object node.</returns>
        int GetObjectPropertyCount(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Tries to get a object property from an object with a particular property name.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of object node to get a property from.</param>
        /// <param name="propertyName">The name of the property to search for.</param>
        /// <param name="objectProperty">The <see cref="ObjectProperty"/> with the specified property name if it exists.</param>
        /// <returns><code>true</code> if the JsonNavigator successfully found the <see cref="IJsonNavigatorNode"/> with the specified property name; <code>false</code> otherwise.</returns>
        bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <summary>
        /// Gets an IEnumerable of <see cref="ObjectProperty"/> properties from an object node.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of object node to get the properties from.</param>
        /// <returns>The IEnumerable of <see cref="ObjectProperty"/> properties from an object node.</returns>
        IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Tries to get the buffered raw json
        /// </summary>
        /// <param name="jsonNode">The json node of interest</param>
        /// <param name="bufferedRawJson">The raw json.</param>
        /// <returns>True if bufferedRawJson was set. False otherwise.</returns>
        bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out IReadOnlyList<byte> bufferedRawJson);
    }
}
