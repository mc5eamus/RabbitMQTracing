using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RabbitMqTracingTest.Diagnostics
{
    public static class RabbitMQMessageExtensions
    {
        public static Activity ExtractActivity(this IBasicProperties msgProps, string activityName = null)
        {
            if (msgProps == null)
            {
                throw new ArgumentNullException(nameof(msgProps));
            }

            if (activityName == null)
            {
                activityName = RabbitMQDiagnosticsSource.ProcessActivityName;
            }

            var activity = new Activity(activityName);

            if(msgProps.IsCorrelationIdPresent())
            {
                activity.SetParentId(msgProps.CorrelationId);
            }

            return activity;
        }
    }
}
