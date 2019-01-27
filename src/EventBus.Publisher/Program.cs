namespace EventBus.Publisher
{
    using System;
    using Castle.Facilities.TypedFactory;
    using Castle.MicroKernel.Registration;
    using EventBus.Events;
    using EventBus.RabbitMQ;
    using Logging.NLog.Impl.Castle;

    class Program
    {
        static void Main()
        {
            var container = new Castle.Windsor.WindsorContainer();

            container.AddFacility<TypedFactoryFacility>();

            container.Install(new LogInstaller());

            container.Register(
                Component.For<IIntegrationEventHandlerFactory>().AsFactory(new IntegrationEventHandlerComponentSelector()),

                Component.For<IEventBusSubscriptionsManager>()
                    .ImplementedBy<InMemoryEventBusSubscriptionsManager>(),

                Component.For<IRabbitMQPersistentConnection>()
                    .ImplementedBy<DefaultRabbitMQPersistentConnection>(),


                Component.For<IEventBus>()
                    .ImplementedBy<EventBusRabbitMQ>());

            var x = container.Resolve<IEventBus>();

            while (Console.ReadLine() != "exit")
            {
                for (int i = 0; i < 10; i++)
                {
                    x.Publish(new TestIntegrationEvent());
                }
            }

            container.Dispose();
        }
    }
}
