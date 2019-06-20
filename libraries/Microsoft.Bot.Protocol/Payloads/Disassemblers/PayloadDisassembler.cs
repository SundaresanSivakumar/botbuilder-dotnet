﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Protocol.PayloadTransport;
using Microsoft.Bot.Protocol.Transport;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Microsoft.Bot.Protocol.Payloads
{
    public abstract class PayloadDisassembler
    {
        protected static JsonSerializer Serializer = JsonSerializer.Create(SerializationSettings.DefaultSerializationSettings);

        private IPayloadSender Sender { get; set; }

        private Stream Stream { get; set; }

        private int? StreamLength { get; set; }

        private int SendOffset { get; set; }

        private Guid Id { get; set; }

        private bool IsEnd { get; set; } = false;

        private TaskCompletionSource<bool> _taskCompletionSource;

        public abstract char Type { get; }

        public PayloadDisassembler(IPayloadSender sender, Guid id)
        {
            Sender = sender;
            Id = id;
            _taskCompletionSource = new TaskCompletionSource<bool>();
        }

        public abstract Task<StreamWrapper> GetStream();

        public async Task Disassemble()
        {
            var w = await GetStream().ConfigureAwait(false);

            Stream = w.Stream;
            StreamLength = w.StreamLength;
            SendOffset = 0;

            await Send().ConfigureAwait(false);
        }

        private Task Send()
        {
            // determine if we know the length we can send and whether we can tell if this is the end
            bool isLengthKnown = IsEnd;
            
            var header = new Header()
            {
                Type = Type,
                Id = Id,
                PayloadLength = 0,      // this value is updated by the sender when isLengthKnown is false
                End = IsEnd             // this value is updated by the sender when isLengthKnown is false
            };

            if (StreamLength.HasValue)
            {
                // determine how many bytes we can send and if we are at the end
                header.PayloadLength = (int)Math.Min(StreamLength.Value - SendOffset, TransportConstants.MaxPayloadLength);
                header.End = SendOffset + header.PayloadLength >= StreamLength.Value;
                isLengthKnown = true;
            }

            Sender.SendPayload(header, Stream, isLengthKnown, OnSent);

            return _taskCompletionSource.Task;
        }

        private async Task OnSent(Header header)
        {
            SendOffset += header.PayloadLength;
            IsEnd = header.End;

            if (IsEnd)
            {
                _taskCompletionSource.SetResult(true);
            }
            else
            {
                await Send().ConfigureAwait(false);
            }
        }

        protected static StreamDescription GetStreamDescription(HttpContentStream stream)
        {
            var description = new StreamDescription()
            {
                Id = stream.Id.ToString("D")
            };

            if (stream.Content.Headers.TryGetValues(HeaderNames.ContentType, out IEnumerable<string> contentType))
            {
                description.Type = contentType?.FirstOrDefault();
            }

            if (stream.Content.Headers.TryGetValues(HeaderNames.ContentLength, out IEnumerable<string> contentLength))
            {
                var value = contentLength?.FirstOrDefault();
                if (value != null && Int32.TryParse(value, out int length))
                {
                    description.Length = length;
                }
            }
            else
            {
                description.Length = (int?)stream.Content.Headers.ContentLength;
            }

            return description;
        }

        protected static void Serialize<T>(T item, out MemoryStream stream, out int length)
        {
            stream = new MemoryStream();
            using (var textWriter = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    Serializer.Serialize(jsonWriter, item);
                    jsonWriter.Flush();
                }
            }
            length = (int)stream.Position;
            stream.Position = 0;
        }
    }
}
