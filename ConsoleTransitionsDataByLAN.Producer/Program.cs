namespace ConsoleTransitionsDataByLAN.Producer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var producer = new TCPProducer();
            while (!await producer.ConnectAsync()) { }
            await producer.StartSending();
        }
    }
}
