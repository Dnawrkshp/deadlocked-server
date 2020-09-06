﻿using DotNetty.Common.Internal.Logging;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Groups;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace Server.Pipeline.Udp
{
    public class ScertDatagramHandler : SimpleChannelInboundHandler<ScertDatagramPacket>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ScertDatagramHandler>();

        public override bool IsSharable => true;


        public Action<IChannel> OnChannelActive;
        public Action<IChannel> OnChannelInactive;
        public Action<IChannel, ScertDatagramPacket> OnChannelMessage;

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            // Detect when client disconnects
            ctx.Channel.CloseCompletion.ContinueWith((x) =>
            {
                Logger.Info("Channel Closed");
                OnChannelInactive?.Invoke(ctx.Channel);
            });

            // Send event upstream
            OnChannelActive?.Invoke(ctx.Channel);
        }

        // The Channel is closed hence the connection is closed
        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            Logger.Info("Client disconnected");

            // Send event upstream
            OnChannelInactive?.Invoke(ctx.Channel);
        }


        protected override void ChannelRead0(IChannelHandlerContext ctx, ScertDatagramPacket message)
        {
            // Send upstream
            OnChannelMessage?.Invoke(ctx.Channel, message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Logger.Error(exception);
        }
    }
}
