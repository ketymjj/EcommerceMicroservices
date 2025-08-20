using System.Threading.Tasks;

namespace Shared.Messaging.Interfaces
{
    /// <summary>
    /// Interface para publicação assíncrona de mensagens
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>
        /// Publica uma mensagem no sistema de mensageria
        /// </summary>
        /// <typeparam name="T">Tipo da mensagem</typeparam>
        /// <param name="message">Objeto da mensagem</param>
        /// <param name="routingKey">Chave de roteamento (opcional)</param>
        /// <param name="exchangeType">Tipo de exchange (default: fanout)</param>
        /// <returns>Task representando a operação assíncrona</returns>
        Task PublishAsync<T>(T message, string routingKey = "", string exchangeType = "fanout") where T : class;

        /// <summary>
        /// Publica uma mensagem com metadados adicionais
        /// </summary>
        /// <typeparam name="T">Tipo da mensagem</typeparam>
        /// <param name="message">Objeto da mensagem</param>
        /// <param name="headers">Metadados adicionais</param>
        /// <param name="routingKey">Chave de roteamento (opcional)</param>
        /// <returns>Task representando a operação assíncrona</returns>
        Task PublishWithHeadersAsync<T>(T message, IDictionary<string, object> headers, string routingKey = "") where T : class;

        /// <summary>
        /// Publica uma mensagem com delay
        /// </summary>
        /// <typeparam name="T">Tipo da mensagem</typeparam>
        /// <param name="message">Objeto da mensagem</param>
        /// <param name="delay">Tempo de delay</param>
        /// <param name="routingKey">Chave de roteamento (opcional)</param>
        /// <returns>Task representando a operação assíncrona</returns>
        Task PublishWithDelayAsync<T>(T message, TimeSpan delay, string routingKey = "") where T : class;

        /// <summary>
        /// Publica uma mensagem com política de retentativa
        /// </summary>
        /// <typeparam name="T">Tipo da mensagem</typeparam>
        /// <param name="message">Objeto da mensagem</param>
        /// <param name="retryCount">Número de tentativas</param>
        /// <param name="routingKey">Chave de roteamento (opcional)</param>
        /// <returns>Task representando a operação assíncrona</returns>
        Task PublishWithRetryAsync<T>(T message, int retryCount, string routingKey = "") where T : class;
    }
}