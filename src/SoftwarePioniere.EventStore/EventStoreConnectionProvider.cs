﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.PersistentSubscriptions;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwarePioniere.Hosting;
using ILogger = Microsoft.Extensions.Logging.ILogger;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace SoftwarePioniere.EventStore
{
    /// <summary>
    ///     Verbindung zum Event Store
    /// </summary>
    public sealed class EventStoreConnectionProvider : IConnectionProvider
    {
        //private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly Lazy<IEventStoreConnection> _connection;

        private readonly ConcurrentDictionary<string, IPAddress> _hostIpAddresses = new ConcurrentDictionary<string, IPAddress>();
        private readonly ILogger _logger;

        private IPEndPoint _httpEndpoint;

        public EventStoreConnectionProvider(ILoggerFactory loggerFactory, IOptions<EventStoreOptions> options)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            Options = options.Value;

            _logger = loggerFactory.CreateLogger(GetType());

            _logger.LogInformation("EventStore Options {@Options}", options.Value.CreateSecured());

            OpsCredentials = new UserCredentials(Options.OpsUsername, Options.OpsPassword);
            AdminCredentials = new UserCredentials(Options.AdminUsername, Options.AdminPassword);

            _connection = new Lazy<IEventStoreConnection>(() => CreateNewConnection(Options.ConnectionSetup));
        }

        /// <summary>
        ///     Admin Verbindungsdaten
        /// </summary>
        public UserCredentials AdminCredentials { get; }

        public EventHandler ConfigurationStateChanged { get; set; }

        public EventHandler ConnectionChanged { get; set; }

        public bool IsConfigured { get; private set; }


        public bool IsConnected { get; private set; }

        ///// <summary>
        ///// Connection als Lazy Objekt
        ///// </summary>
        //public Lazy<IEventStoreConnection> Connection { get; }

        /// <summary>
        ///     Ops Verbindungsdaten
        /// </summary>
        public UserCredentials OpsCredentials { get; }

        /// <summary>
        ///     Einstellungen
        /// </summary>
        public EventStoreOptions Options { get; }

        //private IEventStoreConnection CreateForCluster(ConnectionSettingsBuilder connectionSettingsBuilder)
        //{
        //    var endpoints = Options.ClusterIpEndpoints.Select((x, i) =>
        //    {
        //        var ipa = GetHostIp(x);
        //        var port = Options.TcpPort;

        //        if (Options.ClusterHttpPorts.Length >= i + 1) port = Options.ClusterHttpPorts[i];

        //        _logger.LogTrace($"Creating Cluster IP Endpoint: {ipa}:{port}");
        //        return new IPEndPoint(ipa, port);
        //    });

        //    var clusterSettings = ClusterSettings.Create()
        //            .DiscoverClusterViaGossipSeeds()
        //            .SetGossipTimeout(TimeSpan.FromMilliseconds(500))
        //            .SetGossipSeedEndPoints(endpoints.ToArray())
        //        ;

        //    var con = EventStoreConnection.Create(connectionSettingsBuilder, clusterSettings);
        //    return con;
        //}


        public IEventStoreConnection CreateNewConnection(Action<ConnectionSettingsBuilder> setup = null)
        {
            _logger.LogTrace("Creating Connection");

            IEventStoreConnection con;
            var connectionSettingsBuilder = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying();

            setup?.Invoke(connectionSettingsBuilder);

            //if (!Options.UseCluster)
            //{
            var ipa = GetHostIp(Options.IpEndPoint);

            if (Options.UseSslCertificate)
            {
                _logger.LogInformation("Connecting To GetEventStore: with SSL IP: {0}:{1} // User: {2}", Options.IpEndPoint, Options.ExtSecureTcpPort, Options.OpsUsername);

                var url = $"tcp://{ipa.MapToIPv4()}:{Options.ExtSecureTcpPort}";
                connectionSettingsBuilder.UseSslConnection(Options.SslTargetHost, Options.SslValidateServer);
                var uri = new Uri(url);
                con = EventStoreConnection.Create(connectionSettingsBuilder, uri);
            }
            else
            {
                _logger.LogInformation("Connecting To GetEventStore: without SSL IP: {0}:{1} // User: {2}", Options.IpEndPoint, Options.TcpPort, Options.OpsUsername);

                var url = $"tcp://{ipa.MapToIPv4()}:{Options.TcpPort}";
                var uri = new Uri(url);
                con = EventStoreConnection.Create(connectionSettingsBuilder, uri);
            }
            //}
            //else
            //{
            //    if (Options.UseSslCertificate)
            //        //var ipa = GetHostIp(Options.IpEndPoint);
            //        //   var url = $"tcp://{ipa.MapToIPv4()}:{Options.ExtSecureTcpPort}";
            //        connectionSettingsBuilder.UseSslConnection(Options.SslTargetHost, Options.SslValidateServer);

            //    _logger.LogInformation("Connecting To GetEventStore: for Cluster // User: {0}", Options.OpsUsername);
            //    con = CreateForCluster(connectionSettingsBuilder);
            //}

            RegisterEvents(con);
            //con.ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            return con;
        }

        public PersistentSubscriptionsManager CreatePersistentSubscriptionsManager()
        {
            var manager = new PersistentSubscriptionsManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(Options.OperationTimeoutSeconds));
            return manager;
        }

        public ProjectionsManager CreateProjectionsManager()
        {
            var manager = new ProjectionsManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(Options.OperationTimeoutSeconds));
            return manager;
        }

        public QueryManager CreateQueryManager()
        {
            var manager = new QueryManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(Options.ProjectionOperationTimeoutSeconds), TimeSpan.FromSeconds(Options.QueryTimeoutSeconds));
            return manager;
        }

        public UsersManager CreateUsersManager()
        {
            var manager = new UsersManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(Options.OperationTimeoutSeconds));
            return manager;
        }


        public IEventStoreConnection GetActiveConnection()
        {
            AssertInitialized();
            return _connection.Value;
        }


        //public async Task<IEventStoreConnection> GetActiveConnection()
        //{
        //    //https://blog.cdemi.io/async-waiting-inside-c-sharp-locks/
        //    //await SemaphoreSlim.WaitAsync().ConfigureAwait(false);

        //    //try
        //    //{
        //    if (!_connection.IsValueCreated)
        //    {
        //        var con = _connection.Value;
        //        await con.ConnectAsync().ConfigureAwait(false);
        //        return con;
        //    }
        //    //}
        //    //finally
        //    //{
        //    //    //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
        //    //    //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
        //    //    SemaphoreSlim.Release();
        //    //}


        //    return _connection.Value;
        //}

        private IPAddress GetHostIp(string ipEndpoint)
        {
            _logger.LogTrace("GetHostIp for IpEndPoint {IpEndPoint}", ipEndpoint);

            if (_hostIpAddresses.ContainsKey(ipEndpoint))
                return _hostIpAddresses[ipEndpoint];

            if (!IPAddress.TryParse(ipEndpoint, out var ipa))
            {
                _logger.LogTrace("TryParse IP faulted, Try to lookup DNS");

                var hostIp = Dns.GetHostAddressesAsync(ipEndpoint).ConfigureAwait(false).GetAwaiter().GetResult();
                _logger.LogTrace($"Loaded {hostIp.Length} Host Addresses");
                foreach (var ipAddress in hostIp) _logger.LogTrace($"HostIp {ipAddress}");

                if (hostIp.Length > 0)
                {
                    var hostIpAdress = hostIp.Last();
                    return _hostIpAddresses.GetOrAdd(ipEndpoint, hostIpAdress);
                }

                throw new InvalidOperationException("cannot resolve eventstore ip");
            }

            return _hostIpAddresses.GetOrAdd(ipEndpoint, ipa);
        }

        public IPEndPoint GetHttpIpEndpoint()
        {
            _logger.LogTrace("GetHttpIpEndpoint for IpEndPoint {IpEndPoint}", Options.IpEndPoint);

            if (_httpEndpoint != null)
                return _httpEndpoint;

            var ipa = GetHostIp(Options.IpEndPoint);

            _httpEndpoint = new IPEndPoint(ipa, Options.HttpPort);
            return _httpEndpoint;
        }


        public async Task<bool> IsStreamEmptyAsync(string streamName)
        {
            _logger.LogTrace("IsStreamEmptyAsync {StreamName}", streamName);

            //EventReadResult firstEvent = null;
            //try
            //{
            //    var conn = _provider.Connection.Value.Value;

            //    _logger.LogDebug("Try to read first event for stream to check if not empty:", streamName);
            //    //prüfen, ob es in dem stream ein event gibt. sonst ein dummy event eintragen
            //    firstEvent = conn.ReadEventAsync(streamName, 0, false, _provider.AdminCredentials).ConfigureAwait(false).GetAwaiter().GetResult();

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogDebug("cannot read first event");
            //    _logger.LogError(11, ex, ex.Message);
            //}

            //return Task.FromResult( firstEvent == null);

            var ret = false;

            //try
            //{

            var con = GetActiveConnection();

            var slice = await con.ReadStreamEventsForwardAsync(streamName, 0, 1, false, AdminCredentials).ConfigureAwait(false);
            _logger.LogTrace("StreamExists {StreamName} : SliceStatus: {SliceStatus}", streamName, slice.Status);

            if (slice.Status == SliceReadStatus.StreamNotFound) ret = true;

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogDebug("cannot read first event");
            //    _logger.LogError(11, ex, ex.Message);

            //    ret = true;
            //}

            _logger.LogTrace("IsStreamEmptyAsync {StreamName} {IsEmpty}", streamName, ret);

            return ret;
        }

        private void OnConfigurationStateChanged()
        {
            var handler = ConfigurationStateChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void OnConnectionChanged()
        {
            var handler = ConnectionChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void RegisterEvents(IEventStoreConnection con)
        {
            con.Disconnected += (s, e) =>
            {
                _logger.LogInformation("EventStore Disconnected: {ConnectionName}", e.Connection.ConnectionName);
                IsConnected = false;
                OnConnectionChanged();
            };

            con.Reconnecting += (s, e) => { _logger.LogInformation("EventStore Reconnecting: {ConnectionName}", e.Connection.ConnectionName); };

            con.Connected += (s, e) =>
            {
                _logger.LogInformation("EventStore Connected: {ConnectionName}", e.Connection.ConnectionName);
                IsConnected = true;


                //                if (!_isConfigured)
                //                {
                //                    _isConfigured = true;
                //                    _logger.LogInformation("EventStore Connected: {ConnectionName} - Starting Configuration", e.Connection.ConnectionName);
                //                    _configuration.ConfigureEventStore(this);
                //                }

                OnConnectionChanged();
            };
        }

        public void SetConfigurationState(bool isConfigured)
        {
            IsConfigured = isConfigured;
            OnConfigurationStateChanged();
        }

        private bool _isInitialized;

        private void AssertInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Initialize First");
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var con = _connection.Value;
            await con.ConnectAsync().ConfigureAwait(false);

            _isInitialized = true;

        }
    }
}