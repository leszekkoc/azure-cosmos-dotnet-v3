﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class SkipItemQueryExecutionComponent : ItemQueryExecutionComponentBase
    {
        private int skipCount;

        private SkipItemQueryExecutionComponent(
            CosmosQueryExecutionComponent source, 
            int skipCount)
            : base(source)
        {
            this.skipCount = skipCount;
        }

        public static async Task<SkipItemQueryExecutionComponent> CreateAsync(
            int offsetCount,
            string continuationToken,
            Func<string, Task<CosmosQueryExecutionComponent>> createSourceCallback)
        {
            OffsetContinuationToken offsetContinuationToken;
            if (continuationToken != null)
            {
                offsetContinuationToken = OffsetContinuationToken.Parse(continuationToken);
            }
            else
            {
                offsetContinuationToken = new OffsetContinuationToken(offsetCount, null);
            }

            if (offsetContinuationToken.Offset > offsetCount)
            {
                throw new BadRequestException("offset count in continuation token can not be greater than the offsetcount in the query.");
            }

            return new SkipItemQueryExecutionComponent(
                await createSourceCallback(offsetContinuationToken.SourceToken), 
                offsetContinuationToken.Offset);
        }

        internal override bool IsDone
        {
            get
            {
                return this.Source.IsDone;
            }
        }

        internal override async Task<CosmosElementResponse> DrainAsync(int maxElements, CancellationToken token)
        {
            CosmosElementResponse sourcePage = await base.DrainAsync(maxElements, token);

            // skip the documents but keep all the other headers
            List<CosmosElement> documentsAfterSkip = sourcePage.Skip(this.skipCount).ToList();
            CosmosElementResponse offsetPage = new CosmosElementResponse(
                    result: documentsAfterSkip,
                    count: documentsAfterSkip.Count(),
                    responseHeaders: sourcePage.Headers,
                    useETagAsContinuation: sourcePage.UseETagAsContinuation,
                    disallowContinuationTokenMessage: sourcePage.DisallowContinuationTokenMessage,
                    responseLengthBytes: sourcePage.ResponseLengthBytes);

            int numberOfDocumentsSkipped = sourcePage.Count - documentsAfterSkip.Count;
            this.skipCount -= numberOfDocumentsSkipped;

            if (sourcePage.DisallowContinuationTokenMessage == null)
            {
                if (!this.IsDone)
                {
                    string sourceContinuation = sourcePage.ResponseContinuation;
                    offsetPage.ResponseContinuation = new OffsetContinuationToken(
                        this.skipCount,
                        sourceContinuation).ToString();
                }
                else
                {
                    offsetPage.ResponseContinuation = null;
                }
            }

            return offsetPage;
        }

        /// <summary>
        /// A OffsetContinuationToken is a composition of a source continuation token and how many items to skip from that source.
        /// </summary>
        private struct OffsetContinuationToken
        {
            /// <summary>
            /// Initializes a new instance of the OffsetContinuationToken struct.
            /// </summary>
            /// <param name="offset">The number of items to skip in the query.</param>
            /// <param name="sourceToken">The continuation token for the source component of the query.</param>
            public OffsetContinuationToken(int offset, string sourceToken)
            {
                if (offset < 0)
                {
                    throw new ArgumentException($"{nameof(offset)} must be a non negative number.");
                }

                this.Offset = offset;
                this.SourceToken = sourceToken;
            }

            /// <summary>
            /// The number of items to skip in the query.
            /// </summary>
            [JsonProperty("offset")]
            public int Offset
            {
                get;
            }

            /// <summary>
            /// Gets the continuation token for the source component of the query.
            /// </summary>
            [JsonProperty("sourceToken")]
            public string SourceToken
            {
                get;
            }

            /// <summary>
            /// Parses the OffsetContinuationToken from it's string form.
            /// </summary>
            /// <param name="value">The string form to parse from.</param>
            /// <returns>The parsed OffsetContinuationToken.</returns>
            public static OffsetContinuationToken Parse(string value)
            {
                OffsetContinuationToken result;
                if (!TryParse(value, out result))
                {
                    throw new BadRequestException($"Invalid OffsetContinuationToken: {value}");
                }
                else
                {
                    return result;
                }
            }

            /// <summary>
            /// Tries to parse out the OffsetContinuationToken.
            /// </summary>
            /// <param name="value">The value to parse from.</param>
            /// <param name="offsetContinuationToken">The result of parsing out the token.</param>
            /// <returns>Whether or not the LimitContinuationToken was successfully parsed out.</returns>
            public static bool TryParse(string value, out OffsetContinuationToken offsetContinuationToken)
            {
                offsetContinuationToken = default(OffsetContinuationToken);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    offsetContinuationToken = JsonConvert.DeserializeObject<OffsetContinuationToken>(value);
                    return true;
                }
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} Invalid continuation token {value} for offset~Component, exception: {ex}");
                    return false;
                }
            }

            /// <summary>
            /// Gets the string version of the continuation token that can be passed in a response header.
            /// </summary>
            /// <returns>The string version of the continuation token that can be passed in a response header.</returns>
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }
    }
}