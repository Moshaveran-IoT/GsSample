using Domain;
using Domain.Models;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonController : Controller
{

    private readonly IRabbitMQService _rabbitMQService;

    public PersonController(IRabbitMQService rabbitMQService)
        => this._rabbitMQService = rabbitMQService;

    [HttpPost]
    public void CreatePerson([FromBody] Person person)
        => this.AddToQueue(MqttQueue.Person, person);

    private void AddToQueue(MqttQueue queue, object payload)
    {
        try
        {
            var payloadObject = JObject.FromObject(payload);
            this._rabbitMQService.SendMessage(queue.ToString(), payloadObject.ToString());
        }
        catch (Exception)
        {

        }
    }
}
