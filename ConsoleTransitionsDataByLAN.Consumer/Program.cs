namespace ConsoleTransitionsDataByLAN.Consumer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var consumer = new TCPConsumer();
            await consumer.Start();
        }
    }
}
