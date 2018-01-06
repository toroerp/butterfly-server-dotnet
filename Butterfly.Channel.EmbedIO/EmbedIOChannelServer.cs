﻿/*
 * Copyright 2017 Fireshark Studios, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using NLog;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Net;
using Unosquare.Labs.EmbedIO;

using Butterfly.Util;

namespace Butterfly.Channel.EmbedIO {

    /// <inheritdoc/>
    public class EmbedIOChannelServer : BaseChannelServer {
        public readonly WebServer webServer;

        public EmbedIOChannelServer(WebServer webServer, int mustReceiveHeartbeatMillis = 5000) : base(mustReceiveHeartbeatMillis) {
            this.webServer = webServer;
        }

        protected override void DoStart() {
            this.webServer.RegisterModule(new WebSocketsModule());
            foreach (var listener in this.onNewChannelHandlers) {
                logger.Info($"Listening for WebSocket requests at {listener.path}");
                this.webServer.Module<WebSocketsModule>().RegisterWebSocketsServer(listener.path, new MyWebSocketsServer(this, (webRequest, channel) => {
                    this.AddUnauthenticatedChannel(channel);
                    return true;
                }));
            }
        }
    }

    public class MyWebSocketsServer : WebSocketsServer {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly BaseChannelServer channelServer;
        protected readonly Func<IWebRequest, IChannel, bool> onNewChannel;
        protected readonly ConcurrentDictionary<WebSocketContext, EmbedIOChannel> channelByWebSocketContext = new ConcurrentDictionary<WebSocketContext, EmbedIOChannel>();

        public MyWebSocketsServer(BaseChannelServer channelServer, Func<IWebRequest, IChannel, bool> onNewChannel) {
            this.channelServer = channelServer;
            this.onNewChannel = onNewChannel;
        }

        public override string ServerName => "EmbedIO Channel Server";

        protected override void OnClientConnected(WebSocketContext context) {
            var webRequest = new EmbedIOWebRequest(context);
            logger.Trace($"OnClientConnected():Websocket created for path {webRequest.RequestUri.AbsolutePath}");
            var channel = new EmbedIOChannel(this.channelServer, webRequest, message => {
                this.Send(context, message);
            });
            bool valid = this.onNewChannel(webRequest, channel);
            if (valid) {
                this.channelByWebSocketContext[context] = channel;
            }
        }

        protected override void OnClientDisconnected(WebSocketContext context) {
            this.channelByWebSocketContext.TryRemove(context, out EmbedIOChannel dummyContext);
        }

        protected override void OnFrameReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {
        }

        protected override void OnMessageReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {
            if (this.channelByWebSocketContext.TryGetValue(context, out EmbedIOChannel embedIOChannel)) {
                var text = System.Text.Encoding.UTF8.GetString(rxBuffer);
                try {
                    embedIOChannel.ReceiveMessage(text);
                }
                catch (Exception e) {
                    logger.Trace(e);
                    context.WebSocket.CloseAsync().Wait();
                }
            }
        }

        protected EmbedIOChannel GetEmbedIOChannel(string channelId) {
            return channelServer.GetChannel(channelId) as EmbedIOChannel;
        }
    }

    public class EmbedIOChannel : BaseChannel {
        protected readonly Action<string> send;

        public EmbedIOChannel(BaseChannelServer channelServer, IWebRequest webRequest, Action<string> send) : base(channelServer, webRequest) {
            this.send = send;
        }

        protected override Task SendAsync(string text) {
            //logger.Trace($"Send():channelId={channelId},text={text}");
            this.send(text);
            return Task.FromResult(0);
        }

        protected override void DoDispose() {
            //logger.Trace($"DoDispose():id={this.Id}");
        }

    }

    public class EmbedIOWebRequest : IWebRequest {

        protected readonly WebSocketContext context;

        public EmbedIOWebRequest(WebSocketContext context) {
            this.context = context;
        }

        public Uri RequestUri => context.RequestUri;

        public Dictionary<string, string> Headers => context.Headers.ToDictionary();

        public Dictionary<string, string> PathParams => throw new NotImplementedException();

        public Dictionary<string, string> QueryParams => this.RequestUri.ParseQuery();
    }
}
