using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMqTracingTest.Diagnostics;
using RabbitMqTracingTest.Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMqTracingTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WorkItemController : Controller
    {
        private ILogger log;
        private QueueConnectionConfig config;

        readonly RabbitMQDiagnosticsSource diagnosticSource;

        public WorkItemController(ILogger<WorkItemController> log,
            IOptions<QueueConnectionConfig> config)
        {
            this.log = log;
            this.config = config.Value;

            this.diagnosticSource = new RabbitMQDiagnosticsSource("send");
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok("here we go");
        }

        [HttpPost]
        public IActionResult Post(string payload)
        {

            log.LogInformation($"about to send {payload}");

            var cf = new ConnectionFactory().FromConfig(config);

            using (var rabbitConnection = cf.CreateConnection())
            {
                using (var channel = rabbitConnection.CreateModel())
                {
                    channel.QueueDeclare(queue: "queue1",
                                    durable: false,
                                    exclusive: false,
                                    autoDelete: false,
                                    arguments: null);

                    var body = Encoding.UTF8.GetBytes(payload);

                    var props = channel.CreateBasicProperties();

                    bool isDiagnosticSourceEnabled = RabbitMQDiagnosticsSource.IsEnabled();
                    Activity activity = isDiagnosticSourceEnabled ? this.diagnosticSource.SendStart(props) : null;

                    try
                    {
                        channel.BasicPublish(exchange: "",
                                        routingKey: "queue1",
                                        basicProperties: props,
                                        body: body);
                    }
                    catch (Exception ex)
                    {
                        this.diagnosticSource.ReportException(ex);
                    }
                    finally
                    {
                        this.diagnosticSource.SendStop(activity, null);
                        log.LogInformation($"done sending {payload}");
                    }

                }
            }


            return Ok();
        }
    }
}
