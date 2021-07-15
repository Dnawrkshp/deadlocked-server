using Server.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Server.NAT.Config
{
    public class ServerSettings
    {
        /// <summary>
        /// Port of the NAT server.
        /// </summary>
        public int Port { get; set; } = 10070;

        /// <summary>
        /// Beginning of client relay ports
        /// </summary>
        public int RelayPort { get; set; } = 50500;

        /// <summary>
        /// Max number of relay ports available from the first port RelayPort.
        /// </summary>
        public int RelayPortCount { get; set; } = 500;

        /// <summary>
        /// 
        /// </summary>
        public bool UsePublicIp { get; set; }

        /// <summary>
        /// Time after no messages are received before a NATClient is disconnected.
        /// </summary>
        public int ClientTimeout { get; set; } = 30;

        /// <summary>
        /// 
        /// </summary>
        public int RefreshConfigIntervalMs { get; set; } = 5000;


        /// <summary>
        /// Logging settings.
        /// </summary>
        public LogSettings Logging { get; set; } = new LogSettings();
    }
}
