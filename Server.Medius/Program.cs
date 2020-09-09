﻿using DotNetty.Common.Internal.Logging;
using RT.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RT.Models;
using Server.Medius.Models;
using Server.Database;
using Server.Medius.Config;
using NReco.Logging.File;
using Server.Common.Logging;
using Server.Plugins;
using System.Net.NetworkInformation;

namespace Server.Medius
{
    public class Program
    {
        public const string CONFIG_FILE = "config.json";
        public const string DB_CONFIG_FILE = "db.config.json";
        public const string PLUGINS_PATH = "plugins/";
        public const string KEY = "42424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242424242";

        public readonly static PS2_RSA GlobalAuthKey = new PS2_RSA(
            new BigInteger("10315955513017997681600210131013411322695824559688299373570246338038100843097466504032586443986679280716603540690692615875074465586629501752500179100369237", 10),
            new BigInteger("17", 10),
            new BigInteger("4854567300243763614870687120476899445974505675147434999327174747312047455575182761195687859800492317495944895566174677168271650454805328075020357360662513", 10)
        );

        public readonly static RSA_KEY GlobalAuthPublic = new RSA_KEY(GlobalAuthKey.N.ToByteArrayUnsigned().Reverse().ToArray());
        public readonly static RSA_KEY GlobalAuthPrivate = new RSA_KEY(GlobalAuthKey.D.ToByteArrayUnsigned().Reverse().ToArray());

        public static ServerSettings Settings = new ServerSettings();
        public static DbController Database = new DbController(DB_CONFIG_FILE);

        public static IPAddress SERVER_IP = IPAddress.Parse("192.168.0.178");

        public static MediusManager Manager = new MediusManager();
        public static PluginsManager Plugins = null;

        public static MAS AuthenticationServer = new MAS();
        public static MLS LobbyServer = new MLS();
        public static MPS ProxyServer = new MPS();

        public static int TickMS => 1000 / (Settings?.TickRate ?? 10);

        private static FileLoggerProvider _fileLogger = null;
        private static ulong _sessionKeyCounter = 0;
        private static int sleepMS = 0;
        private static readonly object _sessionKeyCounterLock = (object)_sessionKeyCounter;
        private static DateTime? _lastSuccessfulDbAuth = null;

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Program>();

        static async Task StartServerAsync()
        {
            DateTime lastConfigRefresh = DateTime.UtcNow;
            Stopwatch sleepSw = new Stopwatch();

#if DEBUG
            Stopwatch sw = new Stopwatch();
            int ticks = 0;
#endif

            Logger.Info("Starting medius components...");

            Logger.Info($"Starting MAS on port {AuthenticationServer.Port}.");
            AuthenticationServer.Start();
            Logger.Info($"MAS started.");

            Logger.Info($"Starting MLS on port {LobbyServer.Port}.");
            LobbyServer.Start();
            Logger.Info($"MLS started.");

            Logger.Info($"Starting MPS on port {ProxyServer.Port}.");
            ProxyServer.Start();
            Logger.Info($"MPS started.");

            // 
            Logger.Info("Started.");

            try
            {
#if DEBUG
                sw.Start();
#endif

                while (true)
                {
#if DEBUG
                    ++ticks;
                    if (sw.Elapsed.TotalSeconds > 5f)
                    {
                        // 
                        sw.Stop();
                        float tps = ticks / (float)sw.Elapsed.TotalSeconds;
                        float error = MathF.Abs(Settings.TickRate - tps) / Settings.TickRate;

                        if (error > 0.1f)
                            Logger.Error($"Average TPS: {tps} is {error * 100}% off of target {Settings.TickRate}");

                        sw.Restart();
                        ticks = 0;
                    }
#endif

                    // Attempt to authenticate with the db middleware
                    // We do this every 24 hours to get a fresh new token
                    if ((_lastSuccessfulDbAuth == null || (DateTime.UtcNow - _lastSuccessfulDbAuth.Value).TotalHours > 24))
                    {
                        if (!await Database.Authenticate())
                        {
                            // Log and exit when unable to authenticate
                            Logger.Error("Unable to authenticate with the db middleware server");
                            return;
                        }
                        else
                        {
                            _lastSuccessfulDbAuth = DateTime.UtcNow;
                        }
                    }

                    // 
                    sleepSw.Restart();

                    // Tick
                    await AuthenticationServer.Tick();
                    await LobbyServer.Tick();
                    await ProxyServer.Tick();

                    // Tick manager
                    Manager.Tick();

                    // Tick plugins
                    Plugins.Tick();

                    // Reload config
                    if ((DateTime.UtcNow - lastConfigRefresh).TotalMilliseconds > Settings.RefreshConfigInterval)
                    {
                        RefreshConfig();
                        lastConfigRefresh = DateTime.UtcNow;
                    }

                    await Task.Delay((int)Math.Max(0, sleepMS - sleepSw.ElapsedMilliseconds));
                }
            }
            finally
            {
                await AuthenticationServer.Stop();
                await LobbyServer.Stop();
                await ProxyServer.Stop();
            }
        }

        static void Main(string[] args)
        {
            // 
            Initialize();

            // Add file logger if path is valid
            if (new FileInfo(LogSettings.Singleton.LogPath)?.Directory?.Exists ?? false)
            {
                var loggingOptions = new FileLoggerOptions()
                {
                    Append = false,
                    FileSizeLimitBytes = LogSettings.Singleton.RollingFileSize,
                    MaxRollingFiles = LogSettings.Singleton.RollingFileCount
                };
                InternalLoggerFactory.DefaultFactory.AddProvider(_fileLogger = new FileLoggerProvider(LogSettings.Singleton.LogPath, loggingOptions));
                _fileLogger.MinLevel = Settings.Logging.LogLevel;
            }

            // Optionally add console logger (always enabled when debugging)
#if DEBUG
            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => level >= LogSettings.Singleton.LogLevel, true));
#else
            if (Settings.Logging.LogToConsole)
                InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => level >= LogSettings.Singleton.LogLevel, true));
#endif

            // Initialize plugins
            Plugins = new PluginsManager(PLUGINS_PATH);

            // 
            StartServerAsync().Wait();
        }

        static void Initialize()
        {
            // 
            var serializerSettings = new JsonSerializerSettings()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            // Load settings
            if (File.Exists(CONFIG_FILE))
            {
                // Populate existing object
                JsonConvert.PopulateObject(File.ReadAllText(CONFIG_FILE), Settings, serializerSettings);
            }
            else
            {
                // Save defaults
                File.WriteAllText(CONFIG_FILE, JsonConvert.SerializeObject(Settings, Formatting.Indented));
            }

            // Set LogSettings singleton
            LogSettings.Singleton = Settings.Logging;

            // Determine server ip
            if (Settings.UsePublicIp)
            {
                SERVER_IP = GetLocalIPAddress();
            }
            else
            {
                SERVER_IP = IPAddress.Parse(GetPublicIPAddress());
            }

            // 
            if (Settings.ApplicationIds != null)
            {
                foreach (var appId in Settings.ApplicationIds)
                {
                    Manager.AddChannel(new Channel()
                    {
                        ApplicationId = appId,
                        MaxPlayers = 256,
                        Name = "Default",
                        Type = ChannelType.Lobby
                    });
                }
            }
            else
            {
                Manager.AddChannel(new Channel()
                {
                    ApplicationId = 0,
                    MaxPlayers = 256,
                    Name = "Default",
                    Type = ChannelType.Lobby
                });
            }

            // Load tick time into sleep ms for main loop
            sleepMS = TickMS;
        }

        /// <summary>
        /// 
        /// </summary>
        static void RefreshConfig()
        {
            // 
            var serializerSettings = new JsonSerializerSettings()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };

            // Load settings
            if (File.Exists(CONFIG_FILE))
            {
                // Populate existing object
                JsonConvert.PopulateObject(File.ReadAllText(CONFIG_FILE), Settings, serializerSettings);
            }

            // Update file logger min level
            if (_fileLogger != null)
                _fileLogger.MinLevel = Settings.Logging.LogLevel;

            // Load tick time into sleep ms for main loop
            sleepMS = TickMS;
        }

        /// <summary>
        /// From https://www.c-sharpcorner.com/blogs/how-to-get-public-ip-address-using-c-sharp1
        /// </summary>
        /// <returns></returns>
        static string GetPublicIPAddress()
        {
            String address;
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                address = stream.ReadToEnd();
            }

            int first = address.IndexOf("Address: ") + 9;
            int last = address.LastIndexOf("</body>");
            address = address.Substring(first, last - first);

            return address;
        }

        static IPAddress GetLocalIPAddress()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                return null;

            // order interfaces by speed and filter out down and loopback
            // take first of the remaining
            var firstUpInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback && c.OperationalStatus == OperationalStatus.Up);
            if (firstUpInterface != null)
            {
                var props = firstUpInterface.GetIPProperties();
                // get first IPV4 address assigned to this interface
                return props.UnicastAddresses
                    .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(c => c.Address)
                    .FirstOrDefault();
            }

            return null;
        }

        public static string GenerateSessionKey()
        {
            lock (_sessionKeyCounterLock)
            {
                return (++_sessionKeyCounter).ToString();
            }
        }
    }
}
