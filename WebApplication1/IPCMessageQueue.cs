using System;
using System.Messaging;

namespace Qualivision.InterprocessCommunication
{
    public class IPCMessageQueueBase<T>
    {
        protected readonly MessageQueue comunicator = null;
        protected Action<T> messageReceved = null;

        protected IPCMessageQueueBase(string queueName, Action<T> messageReceved)
        {
            string queuePath = string.Format("{0}\\Private$\\{1}", Environment.MachineName, queueName);
            if (MessageQueue.Exists(queuePath))
            {
                comunicator = new MessageQueue(queuePath);
                comunicator.Purge();
            }
            else
            {
                comunicator = MessageQueue.Create(queuePath, false);
            }
            comunicator.Formatter = new BinaryMessageFormatter();

            if (messageReceved != null)
            {
                SetActionDataReceived(messageReceved);
                //this.messageReceved = messageReceved;
                comunicator.ReceiveCompleted += new ReceiveCompletedEventHandler(comunicator_ReceiveCompleted);
                comunicator.BeginReceive();
            }
        }

        protected void SetActionDataReceived(Action<T> messageReceved)
        {
            this.messageReceved = messageReceved;
        }

        protected void Send(T message)
        {
            comunicator.Purge();
            try
            {
                comunicator.Send(message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void comunicator_ReceiveCompleted(object sender, ReceiveCompletedEventArgs e)
        {
            try
            {
                messageReceved((T)comunicator.EndReceive(e.AsyncResult).Body);
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
            finally
            {
                comunicator.BeginReceive();
            }
        }
    }

    public class IPCMessageQueueServer<T> : IPCMessageQueueBase<T>
    {
        public IPCMessageQueueServer(string queueName, Action<T> messageReceved) : base(queueName, messageReceved) { }

        public new void SetActionDataReceived(Action<T> action) { base.SetActionDataReceived(action); }
    }

    public class IPCMessageQueueClient<T> : IPCMessageQueueBase<T>
    {
        public IPCMessageQueueClient(string queueName) : base(queueName, null) { }

        public new void Send(T message)
        {
            base.Send(message);
        }
    }
}