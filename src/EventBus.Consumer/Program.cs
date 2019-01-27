namespace EventBus.Consumer
{
    using System;
    using Castle.Facilities.TypedFactory;
    using Castle.MicroKernel.Registration;
    using Castle.Windsor;
    using EventBus.Events;
    using EventBus.RabbitMQ;
    using Logging.NLog.Impl.Castle;

    class Program
    {
        static void Main(string[] args)
        {
            var container = new WindsorContainer();

            container.AddFacility<TypedFactoryFacility>();

            container.Install(new LogInstaller());

            container.Register(
                Component.For<IIntegrationEventHandlerFactory>().AsFactory(new IntegrationEventHandlerComponentSelector()),

                Component.For<IEventBusSubscriptionsManager>()
                    .ImplementedBy<InMemoryEventBusSubscriptionsManager>(),

                Component.For<IRabbitMQPersistentConnection>()
                    .ImplementedBy<DefaultRabbitMQPersistentConnection>(),

                Component.For<IEventBus>()
                    .ImplementedBy<EventBusRabbitMQ>()
                    .DependsOn(new { queueName = "servicebus" }),

                Component.For<TestIntegrationEventHandler>());

            var x = container.Resolve<IEventBus>();

            x.Subscribe<TestIntegrationEvent, TestIntegrationEventHandler>();

            x.StartConsumerChannel();

            Console.ReadLine();

            container.Dispose();
        }
    }
}
