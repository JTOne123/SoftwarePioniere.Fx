using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public class EventStoreConnectionProviderTests : TestBase
    {
        // ReSharper disable once UnusedParameter.Local
        private EventStoreConnectionProvider CreateProvider(Action<EventStoreOptions> config = null)
        {
            return GetService<EventStoreConnectionProvider>();
        }

        [Fact]
        public async Task CanConnectToStoreWithOutSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = false);

            await provider.InitializeAsync(CancellationToken.None);
            var con = provider.GetActiveConnection();
            var meta = await con.GetStreamMetadataAsync("$all", provider.AdminCredentials);
            meta.Stream.Should().Be("$all");
        }

        [Fact]
        public async Task CanConnectWithSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = true);

            await provider.InitializeAsync(CancellationToken.None);

            var con = provider.GetActiveConnection();
            var meta = await con.GetStreamMetadataAsync("$all", provider.AdminCredentials);
            meta.Stream.Should().Be("$all");
        }

        [Fact]
        public async Task CanCheckIfStreamIsEmpty()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = false);
            await provider.InitializeAsync(CancellationToken.None);
            var streamId = Guid.NewGuid().ToString().Replace("-", "");
            var empty = await provider.IsStreamEmptyAsync(streamId);
            empty.Should().BeTrue();
        }

        public EventStoreConnectionProviderTests(ITestOutputHelper output) : base(output)
        {
            ServiceCollection
                .AddEventStoreTestConfig(_logger)
                ;

            //var loggerConfiguration = new LoggerConfiguration()
            //        .MinimumLevel.Verbose()
            //        .WriteTo.LiterateConsole()
            //        //#if !DEBUG
            //        .WriteTo.File("/testresults/log.txt")
            //    //#endif

            //    ;
            ////           log.Debug("Test Loggy");

            //var lf = new TestLoggerSerilogFactory(output, loggerConfiguration);
            //ServiceCollection
            //    .AddSingleton<ILoggerFactory>(lf);

            //Log.AddSerilog(loggerConfiguration);

            //output.WriteLine("ctor");
        }
    }
}
