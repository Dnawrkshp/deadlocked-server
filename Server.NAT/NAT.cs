using DotNetty.Buffers;
using DotNetty.Common.Internal.Logging;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using RT.Cryptography;
using RT.Models;
using Server.NAT.Models;
using Server.Pipeline.Udp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Server.NAT
{
    /// <summary>
    /// Unimplemented NAT.
    /// </summary>
    public class NAT
    {
        static readonly IInternalLogger _logger = InternalLoggerFactory.GetInstance<NAT>();

        public int Port => Program.Settings.Port;
        public bool IsRunning => _boundChannel != null && _boundChannel.Active;

        protected ConcurrentDictionary<EndPoint, NATClient> _natClients = new ConcurrentDictionary<EndPoint, NATClient>();

        protected IEventLoopGroup _workerGroup = null;
        protected IChannel _boundChannel = null;
        protected SimpleDatagramHandler _scertHandler = null;
        protected ConcurrentDictionary<int, bool> _ports = new ConcurrentDictionary<int, bool>();
        protected int _rollingPortIndex = 0;

        public NAT()
        {

        }

        /// <summary>
        /// Start the Dme Udp Client Server.
        /// </summary>
        public async Task Start()
        {
            //
            _workerGroup = new MultithreadEventLoopGroup();

            _scertHandler = new SimpleDatagramHandler();

            // Queue all incoming messages
            _scertHandler.OnChannelMessage += async (channel, message) =>
            {
                //_logger.Info($"RECV {message.Sender} {BitConverter.ToString(message.Content.Array, message.Content.ReaderIndex, message.Content.ReadableBytes)}");

#if FALSE
                // Send ip and port back if the last byte isn't 0xD4
                if (message.Content.ReadableBytes == 4 && message.Content.GetByte(message.Content.ReaderIndex + 3) != 0xD4)
                {
                    var buffer = channel.Allocator.Buffer(6);
                    buffer.WriteBytes((message.Sender as IPEndPoint).Address.MapToIPv4().GetAddressBytes());
                    buffer.WriteUnsignedShort((ushort)(message.Sender as IPEndPoint).Port);
                    _ = channel.WriteAndFlushAsync(new DatagramPacket(buffer, message.Sender));
                }
#else
                // Add new client
                if (!_natClients.TryGetValue(message.Sender, out var natClient))
                {
                    // validate message


                    // get next free port
                    var port = AllocatePort();
                    if (!port.HasValue)
                    {
                        _logger.Error($"Unable to allocate port for {message.Sender}. All ports in use.");
                        return;
                    }

                    // 
                    natClient = new NATClient(channel, port.Value, message.Sender);
                    await natClient.Start();
                    _natClients[message.Sender] = natClient;
                    _logger.Info($"Creating new NAT client {natClient.Destination} on port {natClient.Port}");
                }

                await natClient.OnDestinationMessageRecv(channel, message);
#endif
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
                await Task.WhenAll(_natClients.Select(x => x.Value.Stop()));
                _natClients.Clear();
                await _boundChannel.CloseAsync();
            }
            finally
            {
                await Task.WhenAll(
                        _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }

        public async Task Tick()
        {
            // process all natclients
            await Task.WhenAll(_natClients.Select(x => Tick(x.Value)));
        }

        private async Task Tick(NATClient natClient)
        {
            if ((DateTime.UtcNow - natClient.LastMessageUtc).TotalSeconds >= Program.Settings.ClientTimeout)
            {
                _natClients.Remove(natClient.Destination, out _);
                await natClient.Stop();
                FreePort(natClient.Port);
                _logger.Info($"Disconnecting NAT client {natClient.Destination} on port {natClient.Port}");
            }
        }

        public NATClient GetNatClient(EndPoint endpoint)
        {
            if (_natClients.TryGetValue(endpoint, out var client))
                return client;

            return null;
        }

        public List<KeyValuePair<EndPoint, NATClient>> GetNatClients(IPAddress address)
        {
            return _natClients.Where(x => (x.Key as IPEndPoint).Address.Equals(address)).ToList();
        }

        private int? AllocatePort()
        {
            int start = Program.Settings.RelayPort;
            int end = Program.Settings.RelayPortCount + start;
            int rollingStart = _rollingPortIndex;

            // increment and clamp to valid port range
            ++_rollingPortIndex;
            if (_rollingPortIndex < start || _rollingPortIndex >= end)
                _rollingPortIndex = start;

            // find next free port
            for (; _rollingPortIndex < end; ++_rollingPortIndex)
            {
                // loop
                if (_rollingPortIndex >= end)
                    _rollingPortIndex = start;

                // if keypair doesn't exist or is not used, add/update and return free port
                if (!_ports.TryGetValue(_rollingPortIndex, out var isUsed) || !isUsed)
                {
                    _ports.AddOrUpdate(_rollingPortIndex, true, (a, b) => true);
                    return _rollingPortIndex;
                }

                // prevent infinite looping
                if (_rollingPortIndex == rollingStart)
                    return null;
            }

            // no free ports
            return null;
        }

        private void FreePort(int port)
        {
            _ports.AddOrUpdate(port, false, (a, b) => false);
        }
    }
}
