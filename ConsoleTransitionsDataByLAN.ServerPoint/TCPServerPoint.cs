using ConsoleTransitionsDataByLAN.Consumer;
using ConsoleTransitionsDataByLAN.Producer;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsoleTransitionsDataByLAN.ServerPoint
{
    public class TCPServerPoint
    {
        public bool RUN { get; private set; }

        private TCPConsumer _consumer { get; set; }

        public TCPServerPoint()
        {
            RUN = true;
        }

        private string GetOwnIp()
        {
            var ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).SingleOrDefault();
            return ip == null ? "unknown" : ip.ToString();
        }

        public async Task StartServer()
        {
            await Task.Factory.StartNew(async () =>
            {
                _consumer = new TCPConsumer();
                await _consumer.Start();
            });

            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (true)
                    {
                        var receiveCommand = await UDPCommandWatchers.RecieveCommands();
                        await RecieveCommand(receiveCommand);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }

        public async Task RecieveCommand(string command)
        {
            Console.WriteLine($"recieve command: \'{command}\'");
            if (command.StartsWith(CommandManager.listCommand))
            {
                await SendAvailableFileList(command);
            }
            else if (command.StartsWith(CommandManager.fileCommand))
            {
                await SendRequestedFile(command);
            }
            else if (command.StartsWith(CommandManager.errorNotification))
            {
                Console.WriteLine(command);
            }
            else if (command.StartsWith(CommandManager.pingCommand))
            {
                var commandSplit = command.Split(CommandManager.CommandAttributeSeparator);
                if (commandSplit[2] == "0")
                {
                    var refactorCommand = $"{CommandManager.pingCommand}{CommandManager.CommandAttributeSeparator}{GetOwnIp()}{CommandManager.CommandAttributeSeparator}1";
                    await UDPCommandWatchers.SendCommands(refactorCommand, commandSplit[1]);
                }
                else
                {
                    Console.WriteLine($"ping from {commandSplit[1]}");
                }
            }
        }

        public async Task SendCommand(string command)
        {
            if (command.StartsWith(CommandManager.fileCommand))
            {
                var refactorCommand = "";
                if (command.Trim(CommandManager.CommandAttributeSeparator[0]) == CommandManager.fileCommand)
                {
                    refactorCommand = $"{CommandManager.fileCommand}{CommandManager.CommandAttributeSeparator}{GetOwnIp()}{CommandManager.CommandAttributeSeparator}";
                }
                else
                {
                    refactorCommand = $"{CommandManager.fileCommand}{CommandManager.CommandAttributeSeparator}{GetOwnIp()}{CommandManager.CommandAttributeSeparator}{command.Substring(CommandManager.fileCommand.Length + 1)}";
                }
                await UDPCommandWatchers.SendCommands(refactorCommand);
            }
            else if (command == CommandManager.listCommand)
            {
                var refactorCommand = $"{CommandManager.listCommand} {GetOwnIp()}";
                await UDPCommandWatchers.SendCommands($"{command}{CommandManager.CommandAttributeSeparator}{GetOwnIp()}");
            }
            else if (command == CommandManager.helpCommand)
            {
                Console.WriteLine($"type \'{CommandManager.listCommand}\' to get file with list of available file on server\n" +
                    $"type \'{CommandManager.fileCommand} fileName\' to get available file. Example:\'takeF TestHugeFile.txt\n'" +
                    $"type \'ping\' to check wireless TCPServerPoint\n" +
                    $"type \'exit\' to close app");
            }
            else if (command == CommandManager.pingCommand)
            {
                var c = $"{command}{CommandManager.CommandAttributeSeparator}{GetOwnIp()}{CommandManager.CommandAttributeSeparator}0";
                await UDPCommandWatchers.SendCommands(c);
            }
            else if (command == CommandManager.exitCommand)
            {
                Console.WriteLine(CommandManager.exitCommand);
                StopServerPoint();
            }
            else
            {
                Console.WriteLine($"command \'{command}\' not recognized\t type \'{CommandManager.helpCommand}\' for more information");
            }
        }

        private void StopServerPoint()
        {
            RUN = false;
        }

        public Stream AvailableFileList()
        {
            var files = Directory.EnumerateFiles(_consumer.SaveDirrectory, "*", SearchOption.AllDirectories);
            return new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", files.Select(x => Path.GetFileName(x)))));
        }

        public async Task SendAvailableFileList(string command)
        {
            var clientIp = command.Substring(CommandManager.listCommand.Length + 1);
            var availabeFileListStram = AvailableFileList();
            var producer = new TCPProducer(CommandManager.defaultNameAvailableFileList, clientIp);
            while (!await producer.ConnectAsync(availabeFileListStram))
            {
                Console.WriteLine("try connect");
            }
            await producer.StartSending();
        }

        public async Task SendRequestedFile(string command)
        {
            CommandManager.ProcessinFileCommand(command, out string clientIp, out string fileName);

            var files = Directory.EnumerateFiles(_consumer.SaveDirrectory, "*", SearchOption.AllDirectories); ;
            var fullPath = files.SingleOrDefault(x => Path.GetFileName(x) == fileName);

            if (fullPath == null)
            {
                await UDPCommandWatchers.SendCommands(CommandManager.GetErrorMessage(ErrorType.fileNotExist,fileName), clientIp);
                return;
            }

            var producer = new TCPProducer(fullPath, clientIp);
            while (!await producer.ConnectAsync())
            {
                Console.WriteLine("try connect");
            }
            await producer.StartSending();
        }
    }
}
