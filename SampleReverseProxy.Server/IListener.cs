namespace SampleReverseProxy.Server
{
    public interface IListener
    {
        /// <summary>
        /// Send a message to the client.
        /// </summary>
        /// <param name="message"></param>
        void Write(string message);

        /// <summary>
        /// Read a message from the client.
        /// </summary>
        /// <returns></returns>
        string Read();
    }
}
