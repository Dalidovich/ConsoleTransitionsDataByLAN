using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleTransitionsDataByLAN.Consumer
{
    public class TCPConsumer
    {
        public int Port { get; set; }
        public string SaveDirrectory { get; set; }

        private TcpListener _tcpListener { get; set; }

        public TCPConsumer()
        {
            try
            {
                Port = int.Parse(ConfigurationManager.AppSettings["port"]);
                SaveDirrectory = ConfigurationManager.AppSettings["saveDirectoryPath"] ?? @".\";

                _tcpListener = new TcpListener(IPAddress.Any, Port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Init TCPConsumer constructor Error:\n{ex}");
            }
        }

        public async Task Start()
        {
            try
            {
                _tcpListener.Start();
                while (true)
                {
                    var taskId = Guid.NewGuid();
                    var savePathTemp = $"{SaveDirrectory}saveFile_{taskId}_.tempFile";
                    var client = await _tcpListener.AcceptTcpClientAsync();

                    await Task.Factory.StartNew(async () => await StartResieve(client.GetStream(), File.Create(savePathTemp)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Connect TCPConsumer Error:\n{ex}");
            }

        }

        public async Task StartResieve(NetworkStream stream, FileStream fileStream)
        {
            int expectedChunkId = 0;
            var totalSentBytes = 0;

            var taskId = fileStream.Name.Substring(fileStream.Name.LastIndexOf('_') - Guid.NewGuid().ToString().Length, Guid.NewGuid().ToString().Length);

            while (true)
            {
                try
                {
                    byte[] headerBuffer = new byte[8];
                    await stream.ReadExactlyAsync(headerBuffer);

                    //id 4 byte
                    int chunkId = BitConverter.ToInt32(headerBuffer.Take(4).ToArray());
                    //data length 4 byte
                    int chunkLength = BitConverter.ToInt32(headerBuffer.Skip(4).ToArray());

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
                        await _sendACK(true, chunkId, stream);

                        totalSentBytes += buffer.Length;
                        Console.WriteLine($"task: \'{taskId}\'\tchank {chunkId} recieve\t size: \'{buffer.Length}\'");
                    }
                    else if (chunkId < 0 && isHashValid)
                    {
                        var fileName = Encoding.UTF8.GetString(buffer);
                        var savePath = $"{SaveDirrectory}{fileName}";
                        if (File.Exists(savePath))
                        {
                            savePath = Path.Combine(SaveDirrectory,
                                Path.GetFileNameWithoutExtension(fileName) + Guid.NewGuid().ToString() + Path.GetExtension(fileName));
                        }
                        fileStream.Close();
                        File.Move(fileStream.Name, savePath);

                        //send ACK
                        //confirmation bool 1 byte
                        //id 4 byte
                        await _sendACK(true, chunkId, stream);
                        expectedChunkId = 0;
                        break;
                    }
                    else
                    {
                        //send NACK
                        //confirmation bool 1 byte
                        //id 4 byte
                        await _sendACK(false, expectedChunkId, stream);

                        Console.WriteLine($"error in chank {chunkId}; Resend");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{new string('_', 5)}Start TCPConsumer Resieve Error:\n{ex}");
                }
            }
            fileStream.Close();
            stream.Close();
            Console.WriteLine($"\nfile recieve successful\nTotal size of recieve file ~{Math.Round((decimal)totalSentBytes / 1024)} KB");
        }

        private async Task _sendACK(bool confirm, int chunckId, NetworkStream stream)
        {
            byte[] ackPacket = new byte[5];
            ackPacket[0] = (byte)(confirm ? 0x01 : 0x00);
            BitConverter.GetBytes(chunckId).CopyTo(ackPacket, 1);
            await stream.WriteAsync(ackPacket);
        }
    }
}
