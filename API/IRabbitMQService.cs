using RabbitMQ.Client;

using System.Text;

namespace API;

public interface IRabbitMQService
{
    void SendMessage(string queueName, string message);
}

public class RabbitMQService(IConnectionFactory connectionFactory) : IRabbitMQService
{
    public void SendMessage(string queueName, string message)
    {
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
    }
}