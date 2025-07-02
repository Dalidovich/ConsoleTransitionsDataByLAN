using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleTransitionsDataByLAN.Consumer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var produserIp = ConfigurationManager.AppSettings["produserIp"];
            var produserPort = int.Parse(ConfigurationManager.AppSettings["produserPort"]);

            var tcpListener = new TcpListener(IPAddress.Parse(produserIp), produserPort);
            tcpListener.Start();

            var client = await tcpListener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var fileStream = File.Create(@"C:\Users\pops\Downloads\TestHugeFileCopy" + DateTime.Now.ToString("d") + ".txt");

            //buffer for chank recive
            //data + ID + data length + hash
            byte[] buffer = new byte[4 + 4 + 1024 * 1024 + 32];
            int expectedChunkId = 0;

            while (true)
            {
                //id 4 byte
                byte[] chunkIdBuffer = new byte[4];
                int headerBytesRead = await stream.ReadAsync(chunkIdBuffer);
                int chunkId = BitConverter.ToInt32(chunkIdBuffer);

                //connection close
                if (headerBytesRead == 0)
                {
                    break;
                }

                if (Encoding.UTF8.GetString(chunkIdBuffer).StartsWith("END"))
                {
                    break;
                }

                //data length 4 byte
                byte[] chunkLengthBuffer = new byte[4];
                int LengthBufferBytesRead = await stream.ReadAsync(chunkLengthBuffer);
                int chunkLength = BitConverter.ToInt32(chunkLengthBuffer);

                //read data with chunk length
                int bytesRead = await stream.ReadAsync(buffer, 0, chunkLength);

                //read hash 32 byte
                byte[] receivedHash = new byte[32];
                await stream.ReadExactlyAsync(receivedHash);

                //check hash
                byte[] actualHash = SHA256.HashData(buffer.AsSpan(0, bytesRead));
                bool isHashValid = receivedHash.SequenceEqual(actualHash);

                if (chunkId == expectedChunkId && isHashValid)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    expectedChunkId++;

                    //send ACK
                    //confirmation bool 1 byte
                    //id 4 byte
                    byte[] ackPacket = new byte[5];
                    ackPacket[0] = 0x01;
                    BitConverter.GetBytes(chunkId).CopyTo(ackPacket, 1);
                    await stream.WriteAsync(ackPacket);

                    Console.WriteLine($"chank {chunkId} recieve");
                }
                else
                {
                    //send NACK
                    //confirmation bool 1 byte
                    //id 4 byte
                    byte[] nackPacket = new byte[5];
                    nackPacket[0] = 0x00;
                    BitConverter.GetBytes(expectedChunkId).CopyTo(nackPacket, 1);
                    await stream.WriteAsync(nackPacket);

                    Console.WriteLine($"error in chank {chunkId}; Resend");
                }
            }
            Console.WriteLine($"\nfile recieve successful\nTotal size of recieve file ~{expectedChunkId} MB\nPress any to exit...");
            Console.ReadLine();
        }
    }
}
