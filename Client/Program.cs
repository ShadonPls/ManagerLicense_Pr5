using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{

    public class Program
    {
        static IPAddress serverIpAdress;
        static int ServerPort;

        static string ClientToken;
        static DateTime ClientDateConnection;
        static string ClientLogin;

        static void Main(string[] args)
        {
            OnSettings();
            Thread tCheckToken = new Thread(CheckToken);
            tCheckToken.Start();
            while (true)
            {
                SetCommand();
            }
        }

        static void SetCommand()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            string Command = Console.ReadLine();

            if (Command == "/config")
            {
                File.Delete(Directory.GetCurrentDirectory() + "/config");
                OnSettings();
            }
            else if (Command.StartsWith("/connect "))
            {
                ConnectServerWithAuth(Command);
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
        static void ConnectServerWithAuth(string command)
        {
            string[] parts = command.Split(' ');
            if (parts.Length >= 3)
            {
                ClientLogin = parts[1];
                string password = parts[2];

                IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Connect(EndPoint);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                    return;
                }

                if (socket.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connection to server successful");

                    socket.Send(Encoding.UTF8.GetBytes($"/connect {ClientLogin} {password}"));
                    byte[] Bytes = new byte[10485760];
                    int ByteRec = socket.Receive(Bytes);

                    string Response = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                    if (Response == "/limit")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("There is not enough space on the license server");
                    }
                    else if (Response == "/blacklisted")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("User is blacklisted. Access denied.");
                    }
                    else if (Response == "/invalid_password_or_login")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid password.");
                    }
                    else if (Response.StartsWith("/token:"))
                    {
                        ClientToken = Response.Substring(7);
                        ClientDateConnection = DateTime.Now;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Authentication successful. Received token: {ClientToken}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Server response: {Response}");
                    }

                    socket.Close();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Usage: /connect [UserName] [Password]");
            }
        }

        static void ConnectServer()
        {
            IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(EndPoint);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
            }
            if (socket.Connected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Connection to server successful");

                socket.Send(Encoding.UTF8.GetBytes("/token"));
                byte[] Bytes = new byte[10485760];
                int ByteRec = socket.Receive(Bytes);

                string Response = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                if (Response == "/limit")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("There is not enough space on the license server");
                }
                else
                {
                    ClientToken = Response;
                    ClientDateConnection = DateTime.Now;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Received connection token: " + ClientToken);
                }
            }
        }

        static void CheckToken()
        {
            while (true)
            {
                if (string.IsNullOrEmpty(ClientToken))
                {
                    Thread.Sleep(1000);
                    continue;
                }

                IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Connect(EndPoint);

                    socket.Send(Encoding.UTF8.GetBytes(ClientToken));
                    byte[] Bytes = new byte[10485760];
                    int ByteRec = socket.Receive(Bytes);

                    string Response = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                    if (Response == "/disconnect")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The client is disconnected from the server");
                        ClientToken = String.Empty;
                        ClientLogin = String.Empty;
                    }
                }
                catch (SocketException)
                {
                    // Сервер недоступен
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Server is unavailable");
                    ClientToken = String.Empty;
                    ClientLogin = String.Empty;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                }
                finally
                {
                    try { socket.Close(); } catch { }
                }

                Thread.Sleep(1000);
            }
        }

        static void GetStatus()
        {
            if (string.IsNullOrEmpty(ClientToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Not connected to server.");
                return;
            }

            int Duration = (int)DateTime.Now.Subtract(ClientDateConnection).TotalSeconds;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"User: {ClientLogin}, Token: {ClientToken}, " +
                $"Connection time: {ClientDateConnection.ToString("HH:mm:ss dd.MM")}, " +
                $"Duration: {Duration} seconds");
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
                sr.Close();

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Server address: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(IpAdress);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Server port: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(ServerPort.ToString());
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

                StreamWriter streamWriter = new StreamWriter(Path);
                streamWriter.WriteLine(IpAdress);
                streamWriter.WriteLine(ServerPort.ToString());
                streamWriter.Close();
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("To change, write the command: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("/config");
        }

        static void Help()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Commands to the server: ");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/config");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - set initial settings");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/connect [UserName] [Password]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - connect to server with authentication");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/status");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show connection status");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/help");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show this help");
        }
    }
}