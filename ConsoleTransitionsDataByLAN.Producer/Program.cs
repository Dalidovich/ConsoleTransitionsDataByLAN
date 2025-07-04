namespace ConsoleTransitionsDataByLAN.Producer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var producer = new TCPProducer();
            await producer.ConnectAsync();
            await producer.StartSending();
        }
    }
}
