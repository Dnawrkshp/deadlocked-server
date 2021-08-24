using DotNetty.Common.Internal.Logging;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Server.Pipeline.Udp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Server.NAT.Models
{
    public class NATClient
    {

        static readonly IInternalLogger _logger = InternalLoggerFactory.GetInstance<NATClient>();

        public int Port { get; private set; }
        public EndPoint Destination { get; private set; }
        public DateTime LastMessageUtc { get; set; }
        public bool IsRunning => _boundChannel != null && _boundChannel.Active;

        protected IEventLoopGroup _workerGroup = null;
        protected IChannel _boundChannel = null;
        protected IChannel _parentChannel = null;
        protected SimpleDatagramHandler _scertHandler = null;

        protected ConcurrentDictionary<EndPoint, EndPoint> _mappings = new ConcurrentDictionary<EndPoint, EndPoint>();

        public NATClient(IChannel parentChannel, int port, EndPoint destination)
        {
            _parentChannel = parentChannel;
            Port = port;
            Destination = destination;
            LastMessageUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Start the Dme Udp Client Server.
        /// </summary>
        public async Task Start()
        {
            //
            _workerGroup = new MultithreadEventLoopGroup();

            _scertHandler = new SimpleDatagramHandler();

            // Relay all incoming messages
            _scertHandler.OnChannelMessage += (channel, message) =>
            {
                NATClient senderNatClient = null;
                if (_mappings.TryGetValue(message.Sender, out var senderEndpoint))
                    senderNatClient = Program.NATServer.GetNatClient(senderEndpoint);

                // get natclient of sender
                //var senderNatClient = Program.NATServer.GetNatClient(senderEndpoint);
                if (senderNatClient == null)
                {
                    var senders = Program.NATServer.GetNatClients((message.Sender as IPEndPoint).Address);
                    _logger.Warn($"{Port}: Found {senders?.Count ?? -1} nat clients with address {(message.Sender as IPEndPoint).Address}");
                    if (senders != null)
                    {
                        var free = senders.FirstOrDefault(x => !_mappings.Any(y => y.Value == x.Key));
                        if (free.Value != null)
                        {
                            _mappings.TryAdd(message.Sender, free.Key);
                            senderNatClient = free.Value;
                        }
                    }
                }

                if (senderNatClient != null)
                {
                    var dest = senderNatClient._mappings.FirstOrDefault(x => (x.Key as IPEndPoint).Address.Equals((Destination as IPEndPoint).Address));
                    _logger.Info($"{senderNatClient.Port} has mapped dest: {dest.Key} {dest.Value}");

                    var d = dest.Key ?? Destination;

                    var buffer = message.Content.ReadBytes(message.Content.ReadableBytes);
                    buffer.Retain();
                    _ = senderNatClient._boundChannel.WriteAndFlushAsync(new DatagramPacket(buffer, d));

                    _logger.Info($"RELAY ({senderNatClient.Port} to {Port}) {(message.Sender as IPEndPoint).Address.ToString()} {BitConverter.ToString(message.Content.Array, message.Content.ReaderIndex, message.Content.ReadableBytes)}");
                }
                else
                {
                    _logger.Error($"FAILED TO FIND RELAY FOR {message.Sender} on {Port}");
                }
            };

            var bootstrap = new Bootstrap();
            bootstrap
                .Group(_workerGroup)
                .Channel<SocketDatagramChannel>()
                .Handler(new LoggingHandler(LogLevel.INFO))
                .Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;

                    pipeline.AddLast(_scertHandler);
                }));

            _boundChannel = await bootstrap.BindAsync(Port);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public virtual async Task Stop()
        {
            try
            {
                await _boundChannel.CloseAsync();
            }
            finally
            {
                await Task.WhenAll(
                        _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        public virtual async Task OnDestinationMessageRecv(IChannel channel, DatagramPacket message)
        {
            // 
            LastMessageUtc = DateTime.UtcNow;

            if (message.Content.ReadableBytes == 4)
            {
                var a1 = message.Content.GetByte(message.Content.ReaderIndex + 3);
                var a2 = message.Content.GetByte(message.Content.ReaderIndex + 2);

                // send this IP and then the special port
                if (a2 == 0xE4)
                {
                    var buffer = channel.Allocator.Buffer(6);
                    buffer.WriteBytes(Program.SERVER_IP.MapToIPv4().GetAddressBytes());
                    buffer.WriteUnsignedShort((ushort)Port);
                    _ = channel.WriteAndFlushAsync(new DatagramPacket(buffer, message.Recipient, message.Sender));
                    _logger.Info($"Sent {Program.SERVER_IP}:{Port} to {message.Sender}");
                }
            }
        }
    }
}
