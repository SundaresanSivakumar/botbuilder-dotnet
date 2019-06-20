﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Bot.Protocol.StreamingExtensions
{
    public static class BotFrameworkApplicationBuilderExtensions
    {
        /// <summary>
        /// Maps various endpoint handlers for the <see cref="ServiceCollectionExtensions.AddBot{TBot}(IServiceCollection, Action{BotFrameworkOptions})">registered bot</see> into the request execution pipeline using the V4 protocol
        /// </summary>
        /// <param name="applicationBuilder">The application builder that defines the bot's pipeline.<see cref="IApplicationBuilder"/>.</param>
        /// <param name="middlewareSet">The set of middleware the bot executes on each turn. <see cref="MiddlewareSet"/>.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IApplicationBuilder UseBotFrameworkNamedPipe(this IApplicationBuilder applicationBuilder, IList<IMiddleware> middlewareSet = null)
        {
            if (applicationBuilder == null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            var applicationServices = applicationBuilder.ApplicationServices;
            var bot = applicationServices.GetRequiredService<IBot>();
            var connector = applicationServices.GetRequiredService<NamedPipeConnector>();
            connector.InitializeNamedPipeServer(bot, middlewareSet);

            return applicationBuilder;
        }
    }
}
