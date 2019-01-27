namespace EventBus.Consumer
{
    using System.Threading.Tasks;
    using EventBus.Events;
    using Logging;

    public class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        private readonly ILogger _logger;

        public TestIntegrationEventHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task Handle(TestIntegrationEvent @event)
        {
            _logger.Info($"Event {@event.Id} handled.");
            return Task.CompletedTask;
        }
    }
}