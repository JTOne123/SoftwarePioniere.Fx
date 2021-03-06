﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Foundatio.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoftwarePioniere.Domain;
using SoftwarePioniere.Domain.Exceptions;
using SoftwarePioniere.FakeDomain;
using SoftwarePioniere.Messaging;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable RedundantNameQualifier

namespace SoftwarePioniere.Tests.DomainModel.Services
{
    public class RepositoryTests : Domain.TestBase, IRepositoryTests
    {
        private IRepository CreateInstance()
        {
            return GetService<IRepository>();
        }

        [Fact]
        public void SaveWithCancelationThrowsError()
        {
            ServiceCollection.AddSingleton(Mock.Of<IEventStore>())
                .AddSingleton(Mock.Of<IMessagePublisher>());


            var repo = CreateInstance();
            var token = new CancellationToken(true);

            Func<Task> f1 = async () =>
            {
                await repo.SaveAsync(FakeAggregate.Factory.Create(), token);
            };

            f1.Should().Throw<Exception>();
        }

        [Fact]
        public async Task SaveCallsEventStoreSavingAsync()
        {
            var mockStore = new Mock<IEventStore>();

            mockStore.Setup(x =>
                    x.SaveEventsAsync<FakeAggregate>(It.IsAny<string>(), It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<int>())
                )
                .Returns(Task.CompletedTask)
                .Verifiable();

            ServiceCollection.AddSingleton(mockStore.Object)
                .AddSingleton(Mock.Of<IMessagePublisher>());

            var repo = CreateInstance();

            var agg = FakeAggregate.Factory.Create();
            agg.DoFakeEvent("faketext");
            await repo.SaveAsync(agg, -1);

            mockStore.Verify();

        }

        public class MockAdapter : IMessageBusAdapter
        {
            private readonly IMessagePublisher _bus;

            public MockAdapter(IMessagePublisher bus)
            {
                _bus = bus;
            }

            public Task PublishAsync(Type messageType, object message, TimeSpan? delay = null,
                CancellationToken cancellationToken = default)
            {
                return _bus.PublishAsync(messageType, message, delay, cancellationToken);
            }

            public Task PublishAsync<T>(T message, TimeSpan? delay = null,
                CancellationToken cancellationToken = default) where T : class, SoftwarePioniere.Messaging.IMessage
            {
                return _bus.PublishAsync(typeof(T), message, delay, cancellationToken);
            }

            public Task SubscribeMessage<T>(Func<T, Task> handler,
                CancellationToken cancellationToken = default, Func<T, string> lockId = null) where T : class, SoftwarePioniere.Messaging.IMessage
            {
                throw new NotImplementedException();
            }

            public Task SubscribeCommand<T>(Func<T, Task> handler,
                CancellationToken cancellationToken = default, Func<T, string> lockId = null) where T : class, ICommand
            {
                throw new NotImplementedException();
            }

            public Task SubscribeAggregateDomainEvent<TAggregate, TDomainEvent>(Func<TDomainEvent, AggregateTypeInfo<TAggregate>, Task> handler,
                CancellationToken cancellationToken = default, Func<TDomainEvent, AggregateTypeInfo<TAggregate>, string> lockId = null) where TAggregate : IAggregateRoot where TDomainEvent : class, IDomainEvent
            {
                throw new NotImplementedException();
            }

            public Task<MessageResponse> PublishCommandAsync<T>(T cmd, CancellationToken cancellationToken = default) where T : class, ICommand
            {
                throw new NotImplementedException();
            }

            public Task<MessageResponse> PublishCommandsAsync<T>(IEnumerable<T> cmds, CancellationToken cancellationToken = default) where T : class, ICommand
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task EventsWillBePushblishedAfterSavingAsync()
        {

            var mockPublisher = new Mock<IMessagePublisher>();

            mockPublisher.Setup(x => x.PublishAsync(
                    It.IsIn(typeof(FakeEvent), typeof(AggregateDomainEventMessage)),
                    It.IsAny<SoftwarePioniere.Messaging.IMessage>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>())
                )
                .Returns(Task.CompletedTask)
                .Verifiable();


            ServiceCollection
                .AddSingleton(Mock.Of<IEventStore>())
                .AddSingleton(mockPublisher.Object)
                .AddSingleton<IMessageBusAdapter>(new MockAdapter(mockPublisher.Object))
                ;

            var repo = CreateInstance();

            var agg = FakeAggregate.Factory.Create();
            agg.DoFakeEvent("faketext");
            await repo.SaveAsync(agg, -1);

            mockPublisher.Verify();
        }

        [Fact]
        public async Task LoadCreatesAggregateAsync()
        {
            var @event = FakeEvent.Create();
            IList<EventDescriptor> list = new List<EventDescriptor> { new EventDescriptor(@event, 0) };

            var mockStore = new Mock<IEventStore>();
            mockStore.Setup(x => x.GetEventsForAggregateAsync<FakeAggregate>(@event.AggregateId))
                .ReturnsAsync(list);

            ServiceCollection
                .AddSingleton(mockStore.Object)
                .AddSingleton(Mock.Of<IMessagePublisher>())
                ;

            var repo = CreateInstance();


            var agg = await repo.GetByIdAsync<FakeAggregate>(@event.AggregateId);
            agg.AggregateId.Should().Be(@event.AggregateId);
        }

        [Fact]
        public void LoadWithCancelationThrowsError()
        {

            ServiceCollection.AddSingleton(Mock.Of<IEventStore>())
                .AddSingleton(Mock.Of<IMessagePublisher>());

            var repo = CreateInstance();
            var token = new CancellationToken(true);

            Func<Task> f1 = async () =>
            {
                await repo.GetByIdAsync<FakeAggregate>(Guid.NewGuid().ToString(), token);
            };

            f1.Should().Throw<Exception>();

        }

        [Fact]
        public void CheckExistsWithCancelationThrowsError()
        {
            ServiceCollection.AddSingleton(Mock.Of<IEventStore>())
                .AddSingleton(Mock.Of<IMessagePublisher>());


            var repo = CreateInstance();
            var token = new CancellationToken(true);

            Func<Task> f1 = async () =>
            {
                await repo.CheckAggregateExists<FakeAggregate>(Guid.NewGuid().ToString(), token);
            };
            f1.Should().Throw<Exception>();
        }

        [Fact]
        public void LoadThrowsExceptionOnWrongVersionAsync()
        {
            var events = FakeEvent.CreateList(10).ToArray();
            var @event = events[0];

            IList<EventDescriptor> list = new List<EventDescriptor>();
            for (int i = 0; i < events.Length; i++)
            {
                list.Add(new EventDescriptor(events[i], i));
            }

            var mockStore = new Mock<IEventStore>();
            mockStore.Setup(x => x.GetEventsForAggregateAsync<FakeAggregate>(@event.AggregateId))
                .ReturnsAsync(list);

            ServiceCollection
                .AddSingleton(mockStore.Object)
                .AddSingleton(Mock.Of<IMessagePublisher>())
                ;

            var repo = CreateInstance();

            Action act = () => repo.GetByIdAsync<FakeAggregate>(@event.AggregateId, 2).GetAwaiter().GetResult();
            act.Should().Throw<ConcurrencyException>();
        }

        public RepositoryTests(ITestOutputHelper output) : base(output)
        {
            ServiceCollection//.AddSingleton<IMessageBus>(new InMemoryMessageBus())
                .AddSingleton<IRepository, Repository>()
                .AddSingleton(Mock.Of<IMessageBusAdapter>())
                ;

        }
    }
}
