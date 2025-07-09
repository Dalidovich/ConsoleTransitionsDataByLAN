using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleTransitionsDataByLAN.Producer
{
    public class TCPProducer
    {
        public string ConsumerIp { get; set; }
        public int ConsumerPort { get; set; }
        public string FilePath { get; set; }
        public IPEndPoint ConsumerEndPoint { get; set; }

        private TcpClient _tcpClient { get; set; } = new TcpClient();
        private Stream _dataStream { get; set; }
        private NetworkStream _stream { get; set; }

        public int ChunkSize { get; set; } = 1024 * 1024;

        public TCPProducer(string? filePath = null, string? consumerIp = null, int? consumerPort = null)
        {
            try
            {
                ConsumerIp = consumerIp ?? ConfigurationManager.AppSettings["consumerIp"];
                ConsumerPort = consumerPort ?? int.Parse(ConfigurationManager.AppSettings["consumerPort"]);
                FilePath = filePath ?? ConfigurationManager.AppSettings["loadFilePath"];

                ConsumerEndPoint = new IPEndPoint(IPAddress.Parse(ConsumerIp), ConsumerPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Init TCPProducer constructor Error:\n{ex}");
            }
        }

        public async Task<bool> ConnectAsync(Stream? dataStream = null)
        {
            try
            {
                await _tcpClient.ConnectAsync(ConsumerEndPoint);

                _stream = _tcpClient.GetStream();
                _dataStream = dataStream ?? File.OpenRead(FilePath);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Connect TCPProducer Error:\n{ex}");

                return false;
            }
        }

        public async Task StartSending()
        {
            try
            {
                var buffer = new byte[ChunkSize];
                int chunkId = 0;
                bool flagSentFileName = false;
                var totalSentBytes = 0;

                var nameBuffer = Encoding.UTF8.GetBytes($"{Path.GetFileName(FilePath)}");

                while (true)
                {
                    var bytesRead = await _dataStream.ReadAsync(buffer, 0, ChunkSize);
                    if (bytesRead == 0)
                    {
                        //end of file
                        if (!flagSentFileName)
                        {
                            chunkId *= -1;
                            bytesRead = nameBuffer.Length;
                            Array.Copy(nameBuffer, buffer, bytesRead);
                            flagSentFileName = true;
                        }
                        else
                        {
                            break;
                        }
                    }

                    //Packet: [ID][Data len][Data][Hash]
                    //var data = new byte[0];
                    var chunkPacket = new List<byte>();
                    var data = buffer.AsSpan(0, bytesRead).ToArray();
                    byte[] hash = SHA256.HashData(data);

                    chunkPacket.AddRange(BitConverter.GetBytes(chunkId));
                    chunkPacket.AddRange(BitConverter.GetBytes(data.Length));
                    chunkPacket.AddRange(data);
                    chunkPacket.AddRange(hash);
                    Console.WriteLine($"sending packet size:{chunkPacket.Count} with id-{chunkId} and data length = \'{data.Length}\'");

                    //Sending
                    await _stream.WriteAsync(chunkPacket.ToArray());

                    //wait ACK
                    byte[] ackBuffer = new byte[5];
                    await _stream.ReadExactlyAsync(ackBuffer);

                    //confirm
                    bool isAck = ackBuffer[0] == 0x01;
                    int ackChunkId = BitConverter.ToInt32(ackBuffer, 1);

                    if (!isAck || ackChunkId != chunkId)
                    {
                        Console.WriteLine($"resend {chunkId}...");
                        flagSentFileName = false;
                        continue;
                    }

                    Console.WriteLine($"chunk {chunkId} sent.");
                    totalSentBytes += data.Length;
                    chunkId++;
                }
                EndSending(totalSentBytes - nameBuffer.Length);
                _dataStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{new string('_', 5)}Start TCPProducer Sending Error:\n{ex}");
            }
        }

        public void EndSending(int size)
        {
            _tcpClient.Close();
            Console.WriteLine($"\nfile sending ended\nTotal size of sent file ~{Math.Round((decimal)size / 1024)} KB");
        }
    }
}
