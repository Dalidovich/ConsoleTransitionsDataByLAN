using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleTransitionsDataByLAN.Producer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var consumerIp = ConfigurationManager.AppSettings["consumerIp"];
            var consumerPort = int.Parse(ConfigurationManager.AppSettings["consumerPort"]);

            var consumerEndPoint = new IPEndPoint(IPAddress.Parse(consumerIp), consumerPort);

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(consumerEndPoint);
            var stream = tcpClient.GetStream();

            string filePath = ConfigurationManager.AppSettings["filePath"];
            using var fileStream = File.OpenRead(filePath);

            //id (4 byte) +
            //data length (4 byte) +
            //data 1 MB +
            //hash 32 byte (SHA-256)
            const int chunkSize = 1024 * 1024;
            byte[] buffer = new byte[chunkSize];
            int chunkId = 0;

            while (true)
            {
                int bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize);
                if (bytesRead == 0)
                {
                    //end of file
                    break;
                }

                //Packet: [ID][Data len][Data][Hash]
                var chunkPacket = new List<byte>();
                var data = buffer.AsSpan(0, bytesRead).ToArray();
                byte[] hash = SHA256.HashData(data);
                chunkPacket.AddRange(BitConverter.GetBytes(chunkId));
                chunkPacket.AddRange(BitConverter.GetBytes(data.Length));
                chunkPacket.AddRange(data);
                chunkPacket.AddRange(hash);
                Console.WriteLine($"sending packet size:{chunkPacket.Count} with id-{chunkId} and data length = \'{data.Length}\'");

                //Sending
                await stream.WriteAsync(chunkPacket.ToArray());

                //wait ACK
                byte[] ackBuffer = new byte[5];
                await stream.ReadExactlyAsync(ackBuffer);

                //confirm
                bool isAck = ackBuffer[0] == 0x01;
                int ackChunkId = BitConverter.ToInt32(ackBuffer, 1);

                if (!isAck || ackChunkId != chunkId)
                {
                    Console.WriteLine($"resend {chunkId}...");
                    continue;
                }

                Console.WriteLine($"chunk {chunkId} sent.");
                chunkId++;
            }
            await stream.WriteAsync(Encoding.UTF8.GetBytes("END\n"));
            Console.WriteLine($"\nfile sending ended\nTotal size of sent file ~{chunkId} MB\nPress any to exit...");
            Console.ReadLine();
        }
    }
}
