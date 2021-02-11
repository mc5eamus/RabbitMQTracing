using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace RabbitMqTracingTest.Queue
{
    public class QueueConnectionConfig
    {
        public string HostName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
        public int RequestedConnectionTimeout { get; set; }
    }

    public static class Extension
    {
        public static ConnectionFactory FromConfig(this ConnectionFactory cf, QueueConnectionConfig config)
        {
            cf.HostName = config.HostName;
            cf.UserName = config.UserName;
            cf.Password = config.Password;
            cf.Port = config.Port;
            cf.RequestedConnectionTimeout = 
                TimeSpan.FromMilliseconds(config.RequestedConnectionTimeout);
            return cf;
        }
    }
}
