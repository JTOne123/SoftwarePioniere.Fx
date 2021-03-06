﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.Domain;
using SoftwarePioniere.FakeDomain;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable ConsiderUsingConfigureAwait

namespace SoftwarePioniere.EventStore.Tests
{
    public class ProjectionReaderTests : TestBase
    {

        [Fact]
        public async Task ProjectionReaderTest()
        {
           

            var setup = GetService<EventStoreSetup>();
            var store = GetService<IEventStore>();
            var proj = GetService<IEventStoreReader>();

            var prov = GetService<EventStoreConnectionProvider>();
            await prov.InitializeAsync(CancellationToken.None);


            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("FakeCounterProjection.js");

            await setup.AddOpsUserToAdminsAsync();

            if (!await setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                await setup.EnableProjectionAsync("$by_category");
            }
            (await setup.CheckProjectionIsRunningAsync("$by_category")).Should().BeTrue();


            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();
            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();

            var save = FakeEvent.CreateList(155).ToArray();
            await store.SaveEventsAsync<FakeAggregate>(save.First().AggregateId, save, 154);
            await Task.Delay(1500);

            var result = await proj.GetProjectionStateAsync<X1>(name, save.First().AggregateId.Replace("-", ""));

            result.Should().NotBeNull();
            result.Ids.Length.Should().Be(save.Length);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once ClassNeverInstantiated.Global
        public class X1
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string[] Ids { get; set; }
        }

        [Fact]
        public async Task ProjectionReaderTest2()
        {
            var setup = GetService<EventStoreSetup>();
            var store = GetService<IEventStore>();
            var proj = GetService<IEventStoreReader>();

            var prov = GetService<EventStoreConnectionProvider>();
            await prov.InitializeAsync(CancellationToken.None);

            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("FakeCounterProjection.js");

            await setup.AddOpsUserToAdminsAsync();

            if (!await setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                await setup.EnableProjectionAsync("$by_category");
            }
            (await setup.CheckProjectionIsRunningAsync("$by_category")).Should().BeTrue();


            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();
            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();

            var save = FakeEvent.CreateList(155).ToArray();
            await store.SaveEventsAsync<FakeAggregate>(save.First().AggregateId, save, 154);

            await Task.Delay(1500);

            var definition = new
            {
                Ids = new string[0]
            };

            var result = await proj.GetProjectionStateAsyncAnonymousType(name, definition, save.First().AggregateId.Replace("-", ""));

            result.Should().NotBeNull();
            result.Ids.Length.Should().Be(save.Length);
        }


        public ProjectionReaderTests(ITestOutputHelper output) : base(output)
        {
            ServiceCollection
                .AddEventStoreTestConfig(_logger)
                ;

            ServiceCollection
                .AddEventStoreDomainServices()
                ;

        }
    }
}
