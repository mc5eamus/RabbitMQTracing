using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace RabbitMqTracingTest.Diagnostics
{
    /// <summary>
    /// Inspired by https://github.com/Azure/azure-service-bus-dotnet/blob/dev/src/Microsoft.Azure.ServiceBus/ServiceBusDiagnosticsSource.cs
    /// </summary>
    public class RabbitMQDiagnosticsSource
    {

        public const string DiagnosticListenerName = "Microsoft.Azure.ServiceBus";
        public const string BaseActivityName = "Microsoft.Azure.ServiceBus.";

        public const string ExceptionEventName = BaseActivityName + "Exception";
        public const string ProcessActivityName = BaseActivityName + "Process";

        public const string ActivityIdPropertyName = "Diagnostic-Id";
        public const string CorrelationContextPropertyName = "Correlation-Context";

        private static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener(DiagnosticListenerName);
        private readonly string entityPath;

        public RabbitMQDiagnosticsSource(string entityPath)
        {
            this.entityPath = entityPath;
        }

        public static bool IsEnabled()
        {
            return DiagnosticListener.IsEnabled();
        }


        #region Send

        internal Activity SendStart(IBasicProperties msgProps)
        {
            Activity activity = Start("Send", () => new
            {
                Entity = this.entityPath
            });

            Inject(msgProps);

            return activity;
        }

        internal void SendStop(Activity activity, TaskStatus? status)
        {
            if (activity != null)
            {
                DiagnosticListener.StopActivity(activity, new
                {
                    Entity = this.entityPath,
                    Status = status ?? TaskStatus.Faulted
                });
            }
        }

        #endregion


        #region Process

        internal Activity ProcessStart(IBasicProperties msgProps)
        {
            return ProcessStart("Process", msgProps, () => new
            {
                Entity = this.entityPath
            });
        }

        internal void ProcessStop(Activity activity, TaskStatus? status)
        {
            if (activity != null)
            {
                DiagnosticListener.StopActivity(activity, new
                {
                    Entity = this.entityPath,
                    Status = status ?? TaskStatus.Faulted
                });
            }
        }

        #endregion


        internal void ReportException(Exception ex)
        {
            if (DiagnosticListener.IsEnabled(ExceptionEventName))
            {
                DiagnosticListener.Write(ExceptionEventName,
                    new
                    {
                        Exception = ex,
                        Entity = this.entityPath
                    });
            }
        }

        private Activity Start(string operationName, Func<object> getPayload)
        {
            Activity activity = null;
            string activityName = BaseActivityName + operationName;
            if (DiagnosticListener.IsEnabled(activityName, this.entityPath))
            {
                activity = new Activity(activityName);
                if (DiagnosticListener.IsEnabled(activityName + ".Start"))
                {
                    DiagnosticListener.StartActivity(activity, getPayload());
                }
                else
                {
                    activity.Start();
                }
            }

            return activity;
        }

        private void Inject(IBasicProperties msgProps)
        {
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                msgProps.CorrelationId = currentActivity.Id;
            }
        }

        private void Inject(IBasicProperties msgProps, string id)
        {
            if (msgProps != null && !msgProps.IsCorrelationIdPresent())
            {
                msgProps.CorrelationId = id;
            }
        }

        private Activity ProcessStart(string operationName, IBasicProperties msgProps, Func<object> getPayload)
        {
            Activity activity = null;
            string activityName = BaseActivityName + operationName;

            if (msgProps != null && DiagnosticListener.IsEnabled(activityName, entityPath))
            {
                var tmpActivity = msgProps.ExtractActivity(activityName);

                if (DiagnosticListener.IsEnabled(activityName, entityPath, tmpActivity))
                {
                    activity = tmpActivity;
                    if (DiagnosticListener.IsEnabled(activityName + ".Start"))
                    {
                        DiagnosticListener.StartActivity(activity, getPayload());
                    }
                    else
                    {
                        activity.Start();
                    }
                }
            }
            return activity;
        }
    }
}
