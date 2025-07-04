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
        public string SavePathTemp { get; set; }

        private TcpListener _tcpListener { get; set; }
        private FileStream _fileStream { get; set; }
        private NetworkStream _stream { get; set; }

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

        public async Task ConnectAsync()
        {
            try
            {
                _tcpListener.Start();
                SavePathTemp = $"{SaveDirrectory}saveFile.tempFile";
                var client = await _tcpListener.AcceptTcpClientAsync();
                _stream = client.GetStream();
                _fileStream = File.Create(SavePathTemp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Connect TCPConsumer Error:\n{ex}");
            }
        }

        public async Task StartSending()
        {
            try
            {
                int expectedChunkId = 0;
                var totalSentBytes = 0;

                while (true)
                {
                    byte[] headerBuffer = new byte[8];
                    await _stream.ReadExactlyAsync(headerBuffer);

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
                    await _stream.ReadExactlyAsync(buffer);

                    //read hash 32 byte
                    byte[] receivedHash = new byte[32];
                    await _stream.ReadExactlyAsync(receivedHash);

                    //check hash
                    byte[] actualHash = SHA256.HashData(buffer);
                    bool isHashValid = receivedHash.SequenceEqual(actualHash);

                    if (chunkId == expectedChunkId && isHashValid)
                    {
                        await _fileStream.WriteAsync(buffer);
                        expectedChunkId++;

                        //send ACK
                        //confirmation bool 1 byte
                        //id 4 byte
                        await _sendACK(true, chunkId);

                        totalSentBytes += buffer.Length;
                        Console.WriteLine($"chank {chunkId} recieve");
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
                        _fileStream.Close();
                        File.Move(SavePathTemp, savePath);

                        //send ACK
                        //confirmation bool 1 byte
                        //id 4 byte
                        await _sendACK(true, chunkId);
                        break;
                    }
                    else
                    {
                        //send NACK
                        //confirmation bool 1 byte
                        //id 4 byte
                        await _sendACK(false, expectedChunkId);

                        Console.WriteLine($"error in chank {chunkId}; Resend");
                    }
                }
                EndRecieve(totalSentBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Start TCPConsumer Sending Error:\n{ex}");
            }
        }

        public void EndRecieve(int size)
        {
            _fileStream.Close();
            Console.WriteLine($"\nfile recieve successful\nTotal size of recieve file ~{Math.Round((decimal)size / 1024)} KB\nPress any to exit...");
            Console.ReadLine();
        }

        private async Task _sendACK(bool confirm, int chunckId)
        {
            byte[] ackPacket = new byte[5];
            ackPacket[0] = (byte)(confirm ? 0x01 : 0x00);
            BitConverter.GetBytes(chunckId).CopyTo(ackPacket, 1);
            await _stream.WriteAsync(ackPacket);
        }
    }
}
