using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using RabbitMqTracingTest.Queue;
using RabbitMQ.Client.Events;
using System.Text;
using Microsoft.Extensions.Options;
using System.Net.Http;
using RabbitMqTracingTest.Diagnostics;
using System.Diagnostics;

namespace RabbitMqTracingTest.Processing
{
    public class QueueProcessor : BackgroundService
    {
        private ILogger log;
        private QueueConnectionConfig config;
        private RabbitMQDiagnosticsSource diagnosticSource;

        public QueueProcessor(ILogger<QueueProcessor> log,
            IOptions<QueueConnectionConfig> config)
        {
            this.log = log;
            this.config = config.Value;

            this.diagnosticSource = new RabbitMQDiagnosticsSource("receive");
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
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

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += async (model, ea) =>
                    {
                        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                        log.LogInformation($"received {body}");

                        bool isDiagnosticSourceEnabled = RabbitMQDiagnosticsSource.IsEnabled();
                        Activity activity = isDiagnosticSourceEnabled ? this.diagnosticSource.ProcessStart(ea.BasicProperties) : null;
                        try
                        {
                            log.LogInformation($"starting some http call... ");
                            var client = new HttpClient();
                            var msg = await client.GetAsync("https://www.microsoft.com");
                            log.LogInformation($"http call completed ");


                            if (body.StartsWith("+") && body.Length > 1)
                            {
                                var props = channel.CreateBasicProperties();

                                Activity sendActivity = isDiagnosticSourceEnabled ? this.diagnosticSource.SendStart(props) : null;
                                var payload = body.Substring(1);
                                try
                                {
                                    channel.BasicPublish(exchange: "",
                                                 routingKey: "queue1",
                                                 basicProperties: props,
                                                 body: Encoding.UTF8.GetBytes(payload));
                                }
                                catch (Exception ex)
                                {
                                    this.diagnosticSource.ReportException(ex);
                                }
                                finally
                                {
                                    this.diagnosticSource.SendStop(sendActivity, null);
                                    log.LogInformation($"- done routing {payload}");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            this.diagnosticSource.ReportException(e);
                            throw;
                        }
                        finally
                        {
                            this.diagnosticSource.ProcessStop(activity, null);
                        }
                    };

                    channel.BasicConsume(queue: "queue1",
                                         autoAck: true,
                                         consumer: consumer);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

    }
}
