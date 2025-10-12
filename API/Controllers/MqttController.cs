
using System.Runtime.CompilerServices;
using System.Text;

using Domain;

using MQTTnet.AspNetCore.AttributeRouting;

using Newtonsoft.Json.Linq;

namespace API.Controllers;

[MqttController]
[MqttRoute("GsSample")]
public sealed class MqttController : MqttBaseController
{
    private readonly IRabbitMQService _rabbitMQService;

    public MqttController(IRabbitMQService rabbitMQService) => this._rabbitMQService = rabbitMQService;

    [MqttRoute("{IMEI}/Signal")]
    public void Signal(string IMEI) =>
        this.AddToQueue(MqttQueue.Signal, this.Message.Payload, IMEI);

    private void AddToQueue(MqttQueue queue, byte[] payload, string imei, [CallerMemberName] string? caller = null)
    {
        var (payloadObject, isSucceed) = this.InitializePayloadObject(payload, imei, caller);
        if (!isSucceed)
        {
            return;
        }

        try
        {
            this._rabbitMQService.SendMessage(queue.ToString(), payloadObject.ToString());
        }
        catch (Exception)
        {

        }
    }

    private (JObject PayloadObject, bool IsSucceed) InitializePayloadObject(byte[] payload, string imei, [CallerMemberName] string? caller = null)
    {
        try
        {
            var payloadString = Encoding.UTF8.GetString(payload);
            var payloadObject = JObject.Parse(payloadString);
            (payloadObject["IMEI"], payloadObject["CreatedOn"]) = (imei, DateTime.Now);

            return (payloadObject, true);
        }
        catch (Exception)
        {
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(this.Message.Payload);
            }
            catch (Exception)
            {
            }

            return (default!, false);
        }
    }
}