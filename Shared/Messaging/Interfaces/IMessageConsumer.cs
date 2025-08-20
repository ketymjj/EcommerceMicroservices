using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Messaging.Interfaces
{
    /// <summary>
    /// Interface para consumo assíncrono de mensagens
    /// </summary>
    public interface IMessageConsumer : IDisposable
    {
        /// <summary>
        /// Inicia o consumo de mensagens de uma fila
        /// </summary>
        /// <typeparam name="T">Tipo da mensagem esperada</typeparam>
        /// <param name="queueName">Nome da fila</param>
        /// <param name="messageHandler">Delegate para processamento da mensagem</param>
        /// <param name="cancellationToken">Token para cancelamento</param>
        /// <returns>Task representando a operação assíncrona</returns>
        Task StartConsumingAsync<T>(
            string queueName,
            Func<T, Task> messageHandler,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Configura a qualidade de serviço (QoS) para o consumidor
        /// </summary>
        /// <param name="prefetchSize">Tamanho máximo de pré-busca (0 para ilimitado)</param>
        /// <param name="prefetchCount">Número máximo de mensagens para pré-busca</param>
        /// <param name="global">Aplicar globalmente ao canal</param>
        void ConfigureQos(ushort prefetchSize = 0, ushort prefetchCount = 1, bool global = false);

        /// <summary>
        /// Cria uma fila temporária exclusiva
        /// </summary>
        /// <returns>Nome da fila criada</returns>
        string CreateTempQueue();

        /// <summary>
        /// Evento disparado quando ocorre um erro no consumidor
        /// </summary>
        event EventHandler<MessageConsumerErrorEventArgs> OnError;

        /// <summary>
        /// Evento disparado quando o consumidor é iniciado
        /// </summary>
        event EventHandler<MessageConsumerStartedEventArgs> OnStarted;
    }

    /// <summary>
    /// Argumentos para eventos de erro do consumidor
    /// </summary>
    public class MessageConsumerErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string QueueName { get; }
        public string? MessageContent { get; }

        public MessageConsumerErrorEventArgs(Exception ex, string queueName, string? messageContent = null)
        {
            Exception = ex;
            QueueName = queueName;
            MessageContent = messageContent;
        }
    }

    /// <summary>
    /// Argumentos para evento de consumidor iniciado
    /// </summary>
    public class MessageConsumerStartedEventArgs : EventArgs
    {
        public string QueueName { get; }
        public DateTimeOffset StartTime { get; }

        public MessageConsumerStartedEventArgs(string queueName)
        {
            QueueName = queueName;
            StartTime = DateTimeOffset.UtcNow;
        }
    }
}