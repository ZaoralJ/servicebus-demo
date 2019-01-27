namespace EventBus.RabbitMQ
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using EventBus;
    using EventBus.Events;
    using Logging;
    using Newtonsoft.Json;
    using Polly;
    using Polly.Retry;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using IModel = global::RabbitMQ.Client.IModel;

    public class EventBusRabbitMQ : IEventBus
    {
        const string BROKER_NAME = "eventbus";

        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private readonly ILogger _logger;
        private readonly IIntegrationEventHandlerFactory _integrationEventHandlerFactory;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly int _retryCount;
        private IModel _consumerChannel;
        private string _queueName;

        public EventBusRabbitMQ(
            IRabbitMQPersistentConnection persistentConnection,
            ILogger logger,
            IEventBusSubscriptionsManager subsManager,
            IIntegrationEventHandlerFactory integrationEventHandlerFactory,
            string queueName = null, int retryCount = 5)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _integrationEventHandlerFactory = integrationEventHandlerFactory;
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
            _queueName = queueName;
            _retryCount = retryCount;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            return;

            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName,
                    exchange: BROKER_NAME,
                    routingKey: eventName);

                if (_subsManager.IsEmpty)
                {
                    _queueName = string.Empty;
                    _consumerChannel?.Close();
                }
            }
        }

        public void Publish(IntegrationEvent @event)
        {
            _logger.Debug($"Publish {@event}");

            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var policy = RetryPolicy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.Warn(ex.ToString());
                });

            using (var channel = _persistentConnection.CreateModel())
            {
                var eventName = @event.GetType().Name;

                // channel.ExchangeDeclare(BROKER_NAME, "direct");

                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2; // persistent

                    channel.BasicPublish(
                        exchange: BROKER_NAME,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                });
            }
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);
            _subsManager.AddSubscription<T, TH>();
        }

        public void StartConsumerChannel()
        {
            CreateConsumerChannel();
        }

        private void DoInternalSubscription(string eventName)
        {
            return;

            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }

                using (var channel = _persistentConnection.CreateModel())
                {
                    channel.QueueBind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
                }
            }
        }

        public void Unsubscribe<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void Dispose()
        {
            _consumerChannel?.Dispose();

            _subsManager.Clear();
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var channel = _persistentConnection.CreateModel();

            //channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");

            //channel.QueueDeclare(queue: _queueName,
            //                     durable: true,
            //                     exclusive: false,
            //                     autoDelete: false,
            //                     arguments: null);


            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var eventName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);

                if (await ProcessEvent(eventName, message))
                {
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
            };

            channel.BasicConsume(queue: _queueName,
                                 autoAck: false,
                                 consumer: consumer);

            channel.CallbackException += (sender, ea) =>
            {
                _consumerChannel?.Dispose();
                _consumerChannel = CreateConsumerChannel();
            };

            return channel;
        }

        private async Task<bool> ProcessEvent(string eventName, string message)
        {
            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                foreach (var subscription in subscriptions)
                {
                    var handler = _integrationEventHandlerFactory.GetIntegrationEventHandler(subscription.HandlerType.FullName);
                    var eventType = _subsManager.GetEventTypeByName(eventName);
                    var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { integrationEvent });
                }

                return true;
            }

            return false;
        }
    }
}