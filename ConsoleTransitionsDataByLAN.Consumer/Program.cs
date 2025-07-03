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
            var port = int.Parse(ConfigurationManager.AppSettings["port"]);
            var saveDirrectory = ConfigurationManager.AppSettings["saveDirectoryPath"] ?? @".\";

            var savePathTemp = $"{saveDirrectory}saveFile.tempFile";

            var tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();

            var client = await tcpListener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            FileStream fileStream = File.Create(savePathTemp);

            int expectedChunkId = 0;
            var totalSentBytes = 0;

            while (true)
            {
                byte[] headerBuffer = new byte[8];
                await stream.ReadExactlyAsync(headerBuffer);

                //id 4 byte
                int chunkId = BitConverter.ToInt32(headerBuffer.Take(4).ToArray());
                //data length 4 byte
                int chunkLength = BitConverter.ToInt32(headerBuffer.Skip(4).ToArray());
                //if (Encoding.UTF8.GetString(headerBuffer).StartsWith("END"))
                //{
                //    break;
                //}

                //read data with chunk length
                var buffer = new byte[chunkLength];
                await stream.ReadExactlyAsync(buffer);

                //read hash 32 byte
                byte[] receivedHash = new byte[32];
                await stream.ReadExactlyAsync(receivedHash);

                //check hash
                byte[] actualHash = SHA256.HashData(buffer);
                bool isHashValid = receivedHash.SequenceEqual(actualHash);

                if (chunkId == expectedChunkId && isHashValid)
                {
                    await fileStream.WriteAsync(buffer);
                    expectedChunkId++;

                    //send ACK
                    //confirmation bool 1 byte
                    //id 4 byte
                    byte[] ackPacket = new byte[5];
                    ackPacket[0] = 0x01;
                    BitConverter.GetBytes(chunkId).CopyTo(ackPacket, 1);
                    await stream.WriteAsync(ackPacket);

                    totalSentBytes += buffer.Length;
                    Console.WriteLine($"chank {chunkId} recieve");
                }
                else if (chunkId < 0 && isHashValid)
                {
                    var fileName = Encoding.UTF8.GetString(buffer);
                    var savePath = $"{saveDirrectory}{fileName}";
                    if (File.Exists(savePath))
                    {
                        savePath = Path.Combine(saveDirrectory,
                            Path.GetFileNameWithoutExtension(fileName) + Guid.NewGuid().ToString() + Path.GetExtension(fileName));
                    }
                    fileStream.Close();
                    File.Move(savePathTemp, savePath);

                    //send ACK
                    //confirmation bool 1 byte
                    //id 4 byte
                    byte[] ackPacket = new byte[5];
                    ackPacket[0] = 0x01;
                    BitConverter.GetBytes(chunkId).CopyTo(ackPacket, 1);
                    await stream.WriteAsync(ackPacket);

                    break;
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
            fileStream.Close();
            Console.WriteLine($"\nfile recieve successful\nTotal size of recieve file ~{Math.Round((decimal)(totalSentBytes) / 1024)} KB\nPress any to exit...");
            Console.ReadLine();
        }
    }
}
