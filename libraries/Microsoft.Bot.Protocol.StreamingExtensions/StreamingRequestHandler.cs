﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Protocol.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Bot.Protocol.StreamingExtensions
{
    public class StreamingRequestHandler : RequestHandler
    {
        /// <summary>
        /// The StreamingRequestHandler serves as a translation layer between the transport layer and bot adapter.
        /// It receives ReceiveRequests from the transport and provides them to the bot adapter in a form
        /// it is able to build activities out of, which are then handed to the bot itself to processed.
        /// </summary>
        /// <param name="onTurnError">The function to perform on turn errors.</param>
        /// <param name="bot">The bot to be used for all requests to this handler.</param>
        /// <param name="middlewareSet">An optional set of middleware to register with the bot.</param>
        public StreamingRequestHandler(Func<ITurnContext, Exception, Task> onTurnError, IBot bot, IList<IMiddleware> middlewareSet = null)
        {
            Bot = bot;
            MiddlewareSet = middlewareSet;
            UserAgent = GetUserAgent();
        }

        /// <summary>
        /// An overload for use with dependency injection via ServiceProvider, as shown
        /// in DotNet Core samples.
        /// </summary>
        /// <param name="onTurnError">The function to perform on turn errors.</param>
        /// <param name="serviceProvider">The service collection containing the registered IBot type.</param>
        /// <param name="middlewareSet">An optional set of middleware to register with the bot.</param>
        public StreamingRequestHandler(Func<ITurnContext, Exception, Task> onTurnError, IServiceProvider serviceProvider, IList<IMiddleware> middlewareSet = null)
        {
            Services = serviceProvider;
            MiddlewareSet = middlewareSet;
            UserAgent = GetUserAgent();
        }

        public IStreamingTransportServer Server { get; set; }
        public IBot Bot { get; }
        public IServiceProvider Services { get; }
        public IList<IMiddleware> MiddlewareSet { get; }
        private Func<ITurnContext, Exception, Task> OnTurnError { get; set; }
        public string UserAgent { get; private set; }

        /// <summary>
        /// Processes incoming requests and returns the response, if any.
        /// </summary>
        /// <param name="request">A ReceiveRequest from the connected channel.</param>
        /// <returns>A response created by the BotAdapter.</returns>
        public override async Task<Response> ProcessRequestAsync(ReceiveRequest request, object context = null, ILogger<RequestHandler> logger = null)
        {
            var response = new Response();

            try
            {
                if (request == null ||
                    string.IsNullOrEmpty(request.Verb) ||
                    string.IsNullOrEmpty(request.Path))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    logger?.LogInformation("Request missing verb and/or path.");
                }
                else if (string.Equals(request.Verb, Request.GET, StringComparison.InvariantCultureIgnoreCase) &&
                         string.Equals(request.Path, "/api/version", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.SetBody(new VersionInfo() { UserAgent = UserAgent });
                }
                else if (string.Equals(request.Verb, Request.POST, StringComparison.InvariantCultureIgnoreCase) &&
                         string.Equals(request.Path, "/api/messages", StringComparison.InvariantCultureIgnoreCase))
                {
                    var body = await request.ReadBodyAsString().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(body) || request.Streams?.Count == 0)
                    {
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        logger?.LogInformation("Request missing body and/or streams.");
                        return response;
                    }

                    try
                    {
                        var adapter = new BotFrameworkStreamingExtensionsAdapter(Server, MiddlewareSet, logger);
                        var bot = Services?.GetService<IBot>() ?? Bot;

                        if(bot == null)
                        {
                            throw new Exception("Unable to find bot when processing request.");
                        }

                        adapter.OnTurnError = OnTurnError;
                        var invokeResponse = await adapter.ProcessActivityAsync(body, request.Streams, new BotCallbackHandler(bot.OnTurnAsync), CancellationToken.None).ConfigureAwait(false);

                        if (invokeResponse == null)
                        {
                            response.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            response.StatusCode = invokeResponse.Status;
                            if (invokeResponse.Body != null)
                            {
                                response.SetBody(invokeResponse.Body);
                            }
                        }

                        invokeResponse = null;
                    }
                    catch (Exception ex)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        logger?.LogError(ex.Message);
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    logger?.LogInformation($"Unknown verb and path: {request.Verb} {request.Path}");
                }
            }
            catch (Exception e)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                logger?.LogError(e.Message);
            }

            return response;
        }

        private static string GetUserAgent()
        {
            var client = new HttpClient();
            var userAgentHeader = client.DefaultRequestHeaders.UserAgent;

            // The Schema version is 3.1, put into the Microsoft-BotFramework header
            var botFwkProductInfo = new ProductInfoHeaderValue("Microsoft-BotFramework", "3.1");
            if (!userAgentHeader.Contains(botFwkProductInfo))
            {
                userAgentHeader.Add(botFwkProductInfo);
            }

            // The Client SDK Version
            //  https://github.com/Microsoft/botbuilder-dotnet/blob/d342cd66d159a023ac435aec0fdf791f93118f5f/doc/UserAgents.md
            var botBuilderProductInfo = new ProductInfoHeaderValue("BotBuilder", ConnectorClient.GetClientVersion(new ConnectorClient(new Uri("http://localhost"))));
            if (!userAgentHeader.Contains(botBuilderProductInfo))
            {
                userAgentHeader.Add(botBuilderProductInfo);
            }

            // Additional Info.
            // https://github.com/Microsoft/botbuilder-dotnet/blob/d342cd66d159a023ac435aec0fdf791f93118f5f/doc/UserAgents.md
            var userAgent = $"({ConnectorClient.GetASPNetVersion()}; {ConnectorClient.GetOsVersion()}; {ConnectorClient.GetArchitecture()})";
            if (ProductInfoHeaderValue.TryParse(userAgent, out var additionalProductInfo))
            {
                if (!userAgentHeader.Contains(additionalProductInfo))
                {
                    userAgentHeader.Add(additionalProductInfo);
                }
            }

            return userAgentHeader.ToString();
        }
    }
}
