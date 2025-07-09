namespace ConsoleTransitionsDataByLAN.ServerPoint
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var sp = new TCPServerPoint();
            await sp.StartServer();
            while (sp.RUN)
            {
                Console.WriteLine("type command");
                var command = Console.ReadLine() ?? "";
                await sp.SendCommand(command);
            }
        }
    }
}
