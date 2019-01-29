namespace EventBus.Consumer
{
    using System.Threading.Tasks;
    using EventBus.Events;
    using Logging;

    public class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        public TestIntegrationEventHandler(IEventBus eventBus, ILogger logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        public Task Handle(TestIntegrationEvent @event)
        {
            _logger.Info($"Event {@event.Id} handled.");

            _eventBus.Publish(new LogIntegrationEvent
            {
                Message = $"TestIntegrationEvent {@event.Id} proceeded"
            });

            return Task.CompletedTask;
        }
    }
}