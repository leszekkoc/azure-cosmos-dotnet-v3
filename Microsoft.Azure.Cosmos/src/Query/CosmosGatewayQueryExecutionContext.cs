﻿//-----------------------------------------------------------------------
// <copyright file="CosmosDefaultItemQueryExecutionContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Gateway document query execution context for single partition queries where the service interop is not available
    /// </summary>
    /// <remarks>
    /// For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
    /// so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
    /// as the ServiceInterop dll is only available in 64-bit.
    /// </remarks>
    internal sealed class CosmosGatewayQueryExecutionContext : CosmosQueryExecutionContext
    {
        // For a single partition collection the only partition is 0
        private const string SinglePartitionKeyId = "0";

        /// <summary>
        /// Whether or not a continuation is expected.
        /// </summary>
        private readonly SchedulingStopwatch fetchSchedulingMetrics;
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;
        private readonly PartitionRoutingHelper partitionRoutingHelper;
        private readonly CosmosQueryContext queryContext;

        private long retries;
        private CosmosElementResponse lastPage;
        private string ContinuationToken => this.lastPage == null ? this.queryContext.QueryRequestOptions.RequestContinuation : this.lastPage.ResponseContinuation;
        

        public CosmosGatewayQueryExecutionContext(
            CosmosQueryContext cosmosQueryContext)
        {
            if(cosmosQueryContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            this.queryContext = cosmosQueryContext;
            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator(SinglePartitionKeyId);
            this.retries = -1;
            this.partitionRoutingHelper = new PartitionRoutingHelper();
        }

        internal override bool IsDone => this.lastPage != null && string.IsNullOrEmpty(this.lastPage.ResponseContinuation);

        internal override async Task<CosmosElementResponse> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            this.lastPage = await this.ExecuteInternalAsync(cancellationToken);
            return this.lastPage;
        }

        private async Task<CosmosElementResponse> ExecuteInternalAsync(CancellationToken token)
        {
            CollectionCache collectionCache = await this.queryContext.QueryClient.GetCollectionCacheAsync();
            PartitionKeyRangeCache partitionKeyRangeCache = await this.queryContext.QueryClient.GetPartitionKeyRangeCache();
            IDocumentClientRetryPolicy retryPolicyInstance = this.queryContext.QueryClient.GetRetryPolicy();
            retryPolicyInstance = new InvalidPartitionExceptionRetryPolicy(collectionCache, retryPolicyInstance);
            if (this.queryContext.ResourceTypeEnum.IsPartitioned())
            {
                retryPolicyInstance = new PartitionKeyRangeGoneRetryPolicy(
                    collectionCache,
                    partitionKeyRangeCache,
                    PathsHelper.GetCollectionPath(this.queryContext.ResourceLink.OriginalString),
                    retryPolicyInstance);
            }

            return await BackoffRetryUtility<CosmosElementResponse>.ExecuteAsync(
                async () =>
                {
                    this.fetchExecutionRangeAccumulator.BeginFetchRange();
                    ++this.retries;
                    CosmosElementResponse response = await this.ExecuteOnceAsync(retryPolicyInstance, token);
                    if (!string.IsNullOrEmpty(response.Headers[HttpConstants.HttpHeaders.QueryMetrics]))
                    {
                        this.fetchExecutionRangeAccumulator.EndFetchRange(
                            response.ActivityId,
                            response.Count,
                            this.retries);
                    }

                    this.retries = -1;
                    return response;
                },
                retryPolicyInstance,
                token);
        }

        private async Task<CosmosElementResponse> ExecuteOnceAsync(IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            if(this.queryContext.QueryRequestOptions.PartitionKey != null || !this.queryContext.ResourceTypeEnum.IsPartitioned())
            {
                return await this.queryContext.ExecuteQueryAsync(
                    this.queryContext.SqlQuerySpec,
                    cancellationToken,
                    requestEnricher: (cosmosRequestMessage) =>
                    {
                        cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, bool.FalseString);
                    },
                    requestOptionsEnricher: (queryRequestOptions) =>
                    {
                        queryRequestOptions.RequestContinuation = this.ContinuationToken;
                    });
            }

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
            // so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
            // as the ServiceInterop dll is only available in 64-bit.
            return await this.queryContext.ExecuteQueryAsync(
                this.queryContext.SqlQuerySpec,
                cancellationToken,
                requestEnricher: (cosmosRequestMessage) =>
                {
                    cosmosRequestMessage.UseGatewayMode = true;
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, this.queryContext.IsContinuationExpected.ToString());
                },
                requestOptionsEnricher: (queryRequestOptions) =>
                {
                    queryRequestOptions.RequestContinuation = this.ContinuationToken;
                });
        }

        public override void Dispose()
        {
        }
    }
}
