namespace EventBus.RabbitMQ
{
    public interface IIntegrationEventHandlerFactory
    {
        object GetIntegrationEventHandler(string name);
    }
}