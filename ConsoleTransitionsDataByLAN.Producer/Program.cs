using System.Configuration;
using System.Net;
using System.Net.Sockets;

namespace ConsoleTransitionsDataByLAN.Producer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var udpClient = new UdpClient();
            var sendText = "Hello world!";

            var consumerIp = ConfigurationManager.AppSettings["consumerIp"];
            var consumerPort = int.Parse(ConfigurationManager.AppSettings["consumerPort"]);

            var consumerEndPoint = new IPEndPoint(IPAddress.Parse(consumerIp), consumerPort);

            Console.WriteLine($"consumer: {consumerEndPoint}");
            while (true)
            {
                try
                {
                    var ms = new MemoryStream();
                    var binWriter = new BinaryWriter(ms);
                    binWriter.Write(sendText);
                    binWriter.Seek(0, SeekOrigin.Begin);

                    udpClient.Send(ms.ToArray(), (int)ms.Length, consumerEndPoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
