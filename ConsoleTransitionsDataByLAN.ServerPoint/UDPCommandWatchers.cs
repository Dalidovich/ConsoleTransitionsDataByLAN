using System.Configuration;
using System.Net;
using System.Net.Sockets;

namespace ConsoleTransitionsDataByLAN.ServerPoint
{
    public static class UDPCommandWatchers
    {
        public static async Task<string> RecieveCommands()
        {
            var port = int.Parse(ConfigurationManager.AppSettings["port"]);

            var udpClient = new UdpClient(port);
            var receiveText = "";
            var data = await udpClient.ReceiveAsync();
            receiveText = receiveText == "" ? System.Text.Encoding.Default.GetString(data.Buffer).Substring(1) : receiveText;
            udpClient.Close();
            return receiveText;
        }


        public static async Task SendCommands(string command, string? ip = null)
        {
            using var udpClient = new UdpClient();

            var consumerIp = ip ?? ConfigurationManager.AppSettings["consumerIp"];
            var consumerPort = int.Parse(ConfigurationManager.AppSettings["consumerPort"]);

            var consumerEndPoint = new IPEndPoint(IPAddress.Parse(consumerIp), consumerPort);

            try
            {
                var ms = new MemoryStream();
                var binWriter = new BinaryWriter(ms);
                binWriter.Write(command);
                binWriter.Seek(0, SeekOrigin.Begin);

                await udpClient.SendAsync(ms.ToArray(), (int)ms.Length, consumerEndPoint);
                udpClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
