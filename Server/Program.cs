using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        static IPAddress serverIpAdress;
        static int ServerPort;
        static int MaxClient;
        static int Duration;

        static List<Classes.Client> AllClients = new List<Classes.Client>();
        static void Main(string[] args)
        {
            OnSettings();

            Thread tListener = new Thread(ConnectServer);
            tListener.Start();

            Thread tDiconnect = new Thread(CheckDisconnectClient);
            tDiconnect.Start();

            while (true)
            {
                SetCommand();
            }
        }

        static void CheckDisconnectClient()
        {
            while (true)
            {
                for (int iClient = 0; iClient < AllClients.Count; iClient++)
                {
                    int ClientDuration = (int)DateTime.Now.Subtract(AllClients[iClient].DateConnect).TotalSeconds;

                    if (ClientDuration > Duration)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Client : {AllClients[iClient].DateConnect} disconnect from server due to timeout");
                    }
                }
                Thread.Sleep(1000);
            }
        }

        static void Help()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Commands to the clients: ");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/config");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - set initial settings");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/disconnect");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - disconnect users from the server");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/status");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show list users");
        }

        static void GetStatus()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"Count clients: {AllClients.Count}");

            foreach (Classes.Client client in AllClients)
            {
                int Duration = (int)DateTime.Now.Subtract(client.DateConnect).TotalSeconds;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Client: {client.Token}, time connection: {client.DateConnect.ToString("HH:mm:ss dd.MM")}, " +
                    $" duration: {Duration}");
            }

        }

        static void SetCommand()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            string Command = Console.ReadLine();

            // Получаем текущую директорию
            string currentDir = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(currentDir, ".config");

            if (Command == "/config")
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                    Console.WriteLine("Конфигурационный файл удален.");
                }
                else
                {
                    Console.WriteLine("Конфигурационный файл не найден.");
                }
                OnSettings();
            }
            else if (Command.Contains("/disconnect"))
            {
                DisconnectServer(Command);
            }
            else if (Command == "/status")
            {
                GetStatus();
            }
            else if (Command == "/help")
            {
                Help();
            }
            else
            {
                Console.WriteLine("Неизвестная команда. Введите /help для списка команд.");
            }
        }

        static void DisconnectServer(string command)
        {
            try
            {
                string Token = command.Replace("/disconnect ", "");
                Classes.Client DisconnectClient = AllClients.Find(x => x.Token == Token);
                AllClients.Remove(DisconnectClient);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Client: {Token} disconnect from server");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Error: " + ex.Message);
            }

        }

        static void OnSettings()
        {
            string Path = Directory.GetCurrentDirectory() + "/.config";
            string IpAdress = "";

            if (File.Exists(Path))
            {
                StreamReader sr = new StreamReader(Path);
                IpAdress = sr.ReadLine();
                serverIpAdress = IPAddress.Parse(IpAdress);
                ServerPort = int.Parse(sr.ReadLine());
                MaxClient = int.Parse(sr.ReadLine());
                Duration = int.Parse(sr.ReadLine());
                sr.Close();

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Server address: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(IpAdress);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Server port: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(ServerPort.ToString());

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Max count clients: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(MaxClient.ToString());

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Token lifetime: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Duration.ToString());
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please provide the IP address if the license server: ");
                Console.ForegroundColor = ConsoleColor.Green;
                IpAdress = Console.ReadLine();
                serverIpAdress = IPAddress.Parse(IpAdress);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please specify the license server port: ");
                Console.ForegroundColor = ConsoleColor.Green;
                ServerPort = int.Parse(Console.ReadLine());

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please indicate the largest number of clients: ");
                Console.ForegroundColor = ConsoleColor.Green;
                MaxClient = int.Parse(Console.ReadLine());

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Specify the token lifetime: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Duration = int.Parse(Console.ReadLine());

                StreamWriter streamWriter = new StreamWriter(Path);
                streamWriter.WriteLine(IpAdress);
                streamWriter.WriteLine(ServerPort.ToString());
                streamWriter.WriteLine(MaxClient.ToString());
                streamWriter.WriteLine(Duration.ToString());
                streamWriter.Close();
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("To change, write the command: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("/config");
        }

        static string SetCommandClient(string command)
        {
            if (command == "/token")
            {
                if (AllClients.Count < MaxClient)
                {
                    Classes.Client newClient = new Classes.Client();
                    AllClients.Add(newClient);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"New client connection: " + newClient.Token);
                    return newClient.Token;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"There is not enough space on the license server");
                    return "/limit";
                }
            }
            else
            {
                Classes.Client client = AllClients.Find(x => x.Token == command);
                return client != null ? "/connect" : "/disconnect";
            }
            return null;
        }

        static void ConnectServer()
        {
            IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
            Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(EndPoint);
            socketListener.Listen(10);

            while (true)
            {
                Socket Handler = socketListener.Accept();

                byte[] Bytes = new byte[10485760];
                int ByteRec = Handler.Receive(Bytes);

                string Message = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                string Response = SetCommandClient(Message);
                Handler.Send(Encoding.UTF8.GetBytes(Response));
            }
        }
    }
}