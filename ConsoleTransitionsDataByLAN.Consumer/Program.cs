using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;

namespace ConsoleTransitionsDataByLAN.Consumer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //var text = "value1";
            //var ms= new MemoryStream();
            //var binWriter = new BinaryWriter(ms);
            //binWriter.Write(text);
            //binWriter.Seek(0, SeekOrigin.Begin);


            //var st = ms.ToArray();

            //BinaryReader reader = new BinaryReader(new MemoryStream(st));
            //byte[] result = reader.ReadBytes((int)binWriter.BaseStream.Length);

            //var str = System.Text.Encoding.Default.GetString(result);
            //Console.WriteLine(str);

            var port = int.Parse(ConfigurationManager.AppSettings["port"]);
            var client = new UdpClient(port);

            Console.WriteLine($"{string.Join('\n',Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(x=>x.AddressFamily==AddressFamily.InterNetwork))}\n" +
                $"\"press any to continue...\"\n");
            Console.ReadLine();
            Console.WriteLine("started listening");

            var receiveText = "";
            while (true)
            {
                var data = await client.ReceiveAsync();
                receiveText= receiveText==""?System.Text.Encoding.Default.GetString(data.Buffer):receiveText;
                Console.WriteLine(receiveText);
                Console.Title = $"bytes received: {data.Buffer.Length * sizeof(byte)}";
            }
        }
    }
}
