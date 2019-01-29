namespace EventBus.LogConsumer
{
    using System.Threading.Tasks;
    using EventBus.Events;
    using Logging;

    public class LogIntegrationEventHandler : IIntegrationEventHandler<LogIntegrationEvent>
    {
        private readonly ILogger _logger;

        public LogIntegrationEventHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task Handle(LogIntegrationEvent @event)
        {
            _logger.Info($"Event {@event.Id} handled.");
            return Task.CompletedTask;
        }
    }
}