namespace ConsoleTransitionsDataByLAN.ServerPoint
{
    public static class CommandManager
    {
        public const string fileCommand = "takeF";
        public const string listCommand = "takeL";
        public const string helpCommand = "help";
        public const string exitCommand = "exit";
        public const string pingCommand = "ping";

        public const string errorNotification= "Error";
        public const string defaultNameAvailableFileList = "Available File List.txt";
        public const string CommandAttributeSeparator = "_";

        public static void ProcessinFileCommand(string command, out string clientIp, out string fileName)
        {
            var withoutCommand = command.Substring(CommandManager.fileCommand.Length + 1);
            clientIp = withoutCommand.Substring(0, withoutCommand.IndexOf(CommandManager.CommandAttributeSeparator));
            fileName = withoutCommand.Substring(withoutCommand.IndexOf(CommandManager.CommandAttributeSeparator) + 1);
        }

        public static string GetErrorMessage(ErrorType errorType, string? value=null)
        {
            switch (errorType)
            {
                case ErrorType.fileNotExist:
                    return $"{errorNotification}:\nFile with name: \'{value??""}\' not exist";
                default:
                    return $"unknown error with value: \'{value?.ToString()}\'";
            }
        }
    }
    public enum ErrorType 
    {
        fileNotExist
    }
}
