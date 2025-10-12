using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Domain;
using Domain.Models;

namespace Listener;

internal class RabbitMQListenerService
{

    private readonly IModel _channel;
    private readonly IConfiguration _config;
    private readonly IConnection _connection;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IRabbitMQListenerRepository _repository;
    private bool _disposedValue;

    public RabbitMQListenerService(IConnectionFactory connectionFactory, IRabbitMQListenerRepository repository, IConfiguration config)
    {
        this._connectionFactory = connectionFactory;
        this._repository = repository;
        this._config = config;
        this._connection = this._connectionFactory.CreateConnection();
        this._channel = this._connection.CreateModel();
    }

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    private void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing)
            {
                this._connection?.Dispose();
                this._channel?.Dispose();
            }

            this._disposedValue = true;
        }
    }

    public void ListenToQueue(string queueName, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = this._channel.QueueDeclare(queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var prefetchCount = ushort.TryParse(this._config["RabbitMQ:PrefetchCount"], out var p) ? p : (ushort)20;
            this._channel.BasicQos(prefetchSize: 0, prefetchCount: prefetchCount, global: false);

            var consumer = new EventingBasicConsumer(this._channel);
            consumer.Received += async (sender, e) => await this.OnReceivedAsync(sender, e, cancellationToken);

            _ = this._channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

        }
        catch (Exception ex)
        {
        }
    }

    private async Task OnReceivedAsync(object? _, BasicDeliverEventArgs e, CancellationToken cancellationToken)
    {
        if (e == null || e.RoutingKey == null)
        {
            this._channel.BasicAck(e?.DeliveryTag ?? 0, false);
            return;
        }

        var queueName = e.RoutingKey;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (e.Body.IsEmpty)
            {
                this._channel.BasicAck(e.DeliveryTag, false); // Acknowledge the message to remove it from the queue.
                return;
            }

            var body = e.Body.Span;
            var message = Encoding.UTF8.GetString(body);

            await this.ProcessMessageAsync(message, queueName, cancellationToken);

            // Acknowledge the message after successful processing.
            this._channel.BasicAck(e.DeliveryTag, false);

        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            BasicAck(e, queueName, ex);
        }
        catch (Exception ex)
        {
            BasicAck(e, queueName, ex);
        }

        void BasicAck(BasicDeliverEventArgs e, string queueName, Exception ex)
        {
            try
            {
                // General exception handling
                this._channel.BasicAck(e.DeliveryTag, true); // Requeue the message
            }
            catch (Exception e_)
            {
            }
        }
    }

    private async Task ProcessMessageAsync(string message, string queueName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Func<string, Task> handler = queueName switch
        {
            nameof(MqttQueue.Person) => msg => this.SavePerson(msg, cancellationToken),
        };

        await handler(message).ConfigureAwait(false);
    }

    private async Task SavePerson(string msg, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }
        var person = JObject.Parse(msg).ToObject<Person>();
        if (person == null)
        {
            return;
        }
        await this._repository.CreatePerson(person, cancellationToken);
    }
}
