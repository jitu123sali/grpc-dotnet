﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client.Internal;
using Grpc.Shared;
using Microsoft.Extensions.Logging;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Grpc.Net.Client
{
    internal static partial class StreamExtensions
    {
        private const int MessageDelimiterSize = 4; // how many bytes it takes to encode "Message-Length"
        private const int HeaderSize = MessageDelimiterSize + 1; // message length + compression flag

        private static readonly Status SendingMessageExceedsLimitStatus = new Status(StatusCode.ResourceExhausted, "Sending message exceeds the maximum configured message size.");
        private static readonly Status ReceivedMessageExceedsLimitStatus = new Status(StatusCode.ResourceExhausted, "Received message exceeds the maximum configured message size.");
        private static readonly Status NoMessageEncodingMessageStatus = new Status(StatusCode.Internal, "Request did not include grpc-encoding value with compressed message.");
        private static readonly Status IdentityMessageEncodingMessageStatus = new Status(StatusCode.Internal, "Request sent 'identity' grpc-encoding value with compressed message.");
        private static Status CreateUnknownMessageEncodingMessageStatus(string unsupportedEncoding, IEnumerable<string> supportedEncodings)
        {
            return new Status(StatusCode.Unimplemented, $"Unsupported grpc-encoding value '{unsupportedEncoding}'. Supported encodings: {string.Join(", ", supportedEncodings)}");
        }

        public static Task<TResponse?> ReadSingleMessageAsync<TResponse>(
            this Stream responseStream,
            ILogger logger,
            Func<DeserializationContext, TResponse> deserializer,
            string grpcEncoding,
            int? maximumMessageSize,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            return responseStream.ReadMessageCoreAsync(logger, deserializer, grpcEncoding, maximumMessageSize, cancellationToken, true, true);
        }

        public static Task<TResponse?> ReadStreamedMessageAsync<TResponse>(
            this Stream responseStream,
            ILogger logger,
            Func<DeserializationContext, TResponse> deserializer,
            string grpcEncoding,
            int? maximumMessageSize,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            return responseStream.ReadMessageCoreAsync(logger, deserializer, grpcEncoding, maximumMessageSize, cancellationToken, true, false);
        }

        private static async Task<TResponse?> ReadMessageCoreAsync<TResponse>(
            this Stream responseStream,
            ILogger logger,
            Func<DeserializationContext, TResponse> deserializer,
            string grpcEncoding,
            int? maximumMessageSize,
            CancellationToken cancellationToken,
            bool canBeEmpty,
            bool singleMessage)
            where TResponse : class
        {
            try
            {
                Log.ReadingMessage(logger);
                cancellationToken.ThrowIfCancellationRequested();

                // Read the header first
                // - 4 bytes for the content length
                // - 1 byte flag for compression
                var header = new byte[HeaderSize];

                int read;
                var received = 0;
                while ((read = await responseStream.ReadAsync(header, received, header.Length - received, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    received += read;

                    if (received == header.Length)
                    {
                        break;
                    }
                }

                if (received < header.Length)
                {
                    if (received == 0 && canBeEmpty)
                    {
                        Log.NoMessageReturned(logger);
                        return default;
                    }

                    throw new InvalidDataException("Unexpected end of content while reading the message header.");
                }

                var compressed = ReadCompressedFlag(header[0]);

                var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(1));
                if (length > int.MaxValue)
                {
                    throw new InvalidDataException("Message too large.");
                }

                if (length > maximumMessageSize)
                {
                    throw new RpcException(ReceivedMessageExceedsLimitStatus);
                }

                // Read message content until content length is reached
                byte[] messageData;
                if (length > 0)
                {
                    received = 0;
                    messageData = new byte[length];
                    while ((read = await responseStream.ReadAsync(messageData, received, messageData.Length - received, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        received += read;

                        if (received == messageData.Length)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    messageData = Array.Empty<byte>();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (compressed)
                {
                    if (grpcEncoding == null)
                    {
                        throw new RpcException(NoMessageEncodingMessageStatus);
                    }
                    if (string.Equals(grpcEncoding, GrpcProtocolConstants.IdentityGrpcEncoding, StringComparison.Ordinal))
                    {
                        throw new RpcException(IdentityMessageEncodingMessageStatus);
                    }

                    // Performance improvement would be to decompress without converting to an intermediary byte array
                    if (!GrpcProtocolHelpers.TryDecompressMessage(grpcEncoding, GrpcProtocolConstants.CompressionProviders, messageData, out var decompressedMessage))
                    {
                        throw new RpcException(CreateUnknownMessageEncodingMessageStatus(grpcEncoding, GrpcProtocolConstants.CompressionProviders.Select(c => c.EncodingName)));
                    }

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    messageData = decompressedMessage;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                Log.DeserializingMessage(logger, messageData.Length, typeof(TResponse));
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                var deserializationContext = new DefaultDeserializationContext();
                deserializationContext.SetPayload(messageData);
                var message = deserializer(deserializationContext);

                if (singleMessage)
                {
                    // Check that there is no additional content in the stream for a single message
                    // There is no ReadByteAsync on stream. Reuse header array with ReadAsync, we don't need it anymore
                    if (await responseStream.ReadAsync(header, 0, 1).ConfigureAwait(false) > 0)
                    {
                        throw new InvalidDataException("Unexpected data after finished reading message.");
                    }
                }

                Log.ReceivedMessage(logger);
                return message;
            }
            catch (Exception ex)
            {
                Log.ErrorReadingMessage(logger, ex);
                throw;
            }
        }

        private static bool ReadCompressedFlag(byte flag)
        {
            if (flag == 0)
            {
                return false;
            }
            else if (flag == 1)
            {
                return true;
            }
            else
            {
                throw new InvalidDataException("Unexpected compressed flag value in message header.");
            }
        }

        public static async Task WriteMessageAsync<TMessage>(
            this Stream stream,
            ILogger logger,
            TMessage message,
            Action<TMessage, SerializationContext> serializer,
            string grpcEncoding,
            int? maximumMessageSize,
            CancellationToken cancellationToken)
        {
            try
            {
                Log.SendingMessage(logger);

                // Serialize message first. Need to know size to prefix the length in the header
                var serializationContext = new DefaultSerializationContext();
                serializer(message, serializationContext);
                var data = serializationContext.Payload;

                if (data == null)
                {
                    throw new InvalidOperationException("Serialization did not return a payload.");
                }

                if (data.Length > maximumMessageSize)
                {
                    throw new RpcException(SendingMessageExceedsLimitStatus);
                }

                var isCompressed = !string.Equals(grpcEncoding, GrpcProtocolConstants.IdentityGrpcEncoding, StringComparison.Ordinal);

                if (isCompressed)
                {
                    data = GrpcProtocolHelpers.CompressMessage(
                        grpcEncoding,
                        CompressionLevel.Fastest,
                        GrpcProtocolConstants.CompressionProviders,
                        data);
                }

                Log.SerializedMessage(logger, typeof(TMessage), data.Length);

                await WriteHeaderAsync(stream, data.Length, isCompressed, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                Log.MessageSent(logger);
            }
            catch (Exception ex)
            {
                Log.ErrorSendingMessage(logger, ex);
                throw;
            }
        }

        private static Task WriteHeaderAsync(Stream stream, int length, bool compress, CancellationToken cancellationToken)
        {
            var headerData = new byte[HeaderSize];

            // Compression flag
            headerData[0] = compress ? (byte)1 : (byte)0;

            // Message length
            EncodeMessageLength(length, headerData.AsSpan(1));

            return stream.WriteAsync(headerData, 0, headerData.Length, cancellationToken);
        }

        private static void EncodeMessageLength(int messageLength, Span<byte> destination)
        {
            Debug.Assert(destination.Length >= MessageDelimiterSize, "Buffer too small to encode message length.");

            BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)messageLength);
        }
    }
}
