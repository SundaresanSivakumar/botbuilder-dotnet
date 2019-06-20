﻿using System;
using System.Collections.Generic;

namespace Microsoft.Bot.Protocol
{
    public class ReceiveResponse
    {
        /// <summary>
        /// Status - The Response Status
        /// </summary>
        public int StatusCode { get; set; }


        public List<IContentStream> Streams { get; set; }
    }
}
