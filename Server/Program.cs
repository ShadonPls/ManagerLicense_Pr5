using Microsoft.EntityFrameworkCore;
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
        private static Data.Context dbContext;
        static IPAddress serverIpAdress;
        static int ServerPort;
        static int MaxClient;
        static int Duration;

        static List<Classes.Client> AllClients = new List<Classes.Client>();
        static Dictionary<string, Socket> ClientSockets = new Dictionary<string, Socket>();

        static void Main(string[] args)
        {
            OnSettings();
            InitializeDbContext();

            Thread tListener = new Thread(ConnectServer);
            tListener.Start();

            Thread tDiconnect = new Thread(CheckDisconnectClient);
            tDiconnect.Start();

            while (true)
            {
                SetCommand();
            }
        }

        static void InitializeDbContext()
        {
            try
            {
                dbContext = new Data.Context();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Database connection established successfully");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Database connection error: {ex.Message}");
                Console.WriteLine("Please check PostgreSQL connection settings in Context.cs");
                Environment.Exit(1);
            }
        }

        static void CheckDisconnectClient()
        {
            while (true)
            {
                try
                {
                    for (int iClient = 0; iClient < AllClients.Count; iClient++)
                    {
                        var client = AllClients[iClient];
                        int ClientDuration = (int)DateTime.Now.Subtract(client.DateConnect.ToLocalTime()).TotalSeconds;
                        if (ClientDuration > Duration)
                        {
                            DisconnectClient(client, "timeout");
                            iClient--;
                        }
                        else if (client.IsBlacklisted)
                        {
                            DisconnectClient(client, "blacklisted");
                            iClient--;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error in CheckDisconnectClient: {ex.Message}");
                }

                Thread.Sleep(1000);
            }
        }

        static void Help()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Commands to the clients: ");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/config");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - set initial settings");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/disconnect [token]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - disconnect specific user");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/status");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show list users");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/blacklist [login]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - manage blacklist [login]");
        }

        static void GetStatus()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Count clients: {AllClients.Count}");

            foreach (Classes.Client client in AllClients)
            {
                DateTime localTime = client.DateConnect.ToLocalTime();
                int Duration = (int)DateTime.Now.Subtract(localTime).TotalSeconds;

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Client: {client.Login} ({client.Token}), " +
                    $"time connection: {localTime.ToString("HH:mm:ss dd.MM.yyyy")}, " +
                    $"duration: {Duration} seconds");
            }
        }

        static void SetCommand()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            string Command = Console.ReadLine();

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
            else if (Command.Contains("/disconnect "))
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
            else if (Command.StartsWith("/blacklist"))
            {
                ManageBlacklist(Command);
            }
            else
            {
                Console.WriteLine("Неизвестная команда. Введите /help для списка команд.");
            }
        }

        static void ManageBlacklist(string command)
        {
            try
            {
                string[] parts = command.Split(' ');
                if (parts.Length < 2)
                {
                    Console.WriteLine("Использование: /blacklist [login] - добавить/удалить из черного списка");
                    return;
                }

                string login = parts[1];
                var user = dbContext.Clients.FirstOrDefault(c => c.Login == login);

                if (user != null)
                {
                    user.IsBlacklisted = !user.IsBlacklisted;
                    dbContext.SaveChanges();
                    if (user.IsBlacklisted)
                    {
                        var connectedClient = AllClients.Find(c => c.Login == login);
                        if (connectedClient != null)
                        {
                            DisconnectClient(connectedClient, "added to blacklist");
                        }
                    }

                    Console.ForegroundColor = user.IsBlacklisted ? ConsoleColor.Red : ConsoleColor.Green;
                    Console.WriteLine($"Пользователь {login} " +
                        (user.IsBlacklisted ? "добавлен в черный список" : "удален из черного списка"));
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Пользователь {login} не найден");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        static void DisconnectServer(string command)
        {
            try
            {
                string Token = command.Replace("/disconnect ", "");
                if (string.IsNullOrEmpty(Token))
                {
                    Console.WriteLine("Usage: /disconnect [token]");
                    return;
                }

                Classes.Client clientToDisconnect = AllClients.Find(x => x.Token == Token);
                if (clientToDisconnect != null)
                {
                    DisconnectClient(clientToDisconnect, "manual disconnect");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Client with token {Token} not found");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        static void DisconnectClient(Classes.Client client, string reason)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Client {client.Login} ({client.Token}) disconnected. Reason: {reason}");
                if (ClientSockets.ContainsKey(client.Token))
                {
                    try
                    {
                        ClientSockets[client.Token].Close();
                    }
                    catch { }
                    ClientSockets.Remove(client.Token);
                }
                AllClients.Remove(client);

                client.Token = "";
                client.DateConnect = DateTime.UtcNow;
                dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error disconnecting client: {ex.Message}");
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
                Console.Write("Token lifetime (seconds): ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Duration.ToString());
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please provide the IP address of the license server: ");
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
                Console.Write("Specify the token lifetime (in seconds): ");
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
            Console.WriteLine("To change settings, write the command: /config");
        }


        static string SetCommandClient(string command, Socket clientSocket = null)
        {
            if (command.StartsWith("/connect "))
            {
                string[] parts = command.Split(' ');
                if (parts.Length >= 3)
                {
                    string login = parts[1];
                    string password = parts[2];

                    var user = dbContext.Clients.FirstOrDefault(c => c.Login == login);

                    if (user != null)
                    {
                        if (user.IsBlacklisted)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"User {login} tried to connect but is blacklisted!");
                            return "/blacklisted";
                        }

                        if (user.Password == password)
                        {
                            if (AllClients.Count < MaxClient)
                            {
                                var existingClient = AllClients.Find(c => c.Login == login);
                                if (existingClient != null)
                                {
                                    DisconnectClient(existingClient, "new connection from same user");
                                }

                                user.Token = GenerateToken();
                                user.DateConnect = DateTime.UtcNow;

                                try
                                {
                                    dbContext.SaveChanges();
                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"Save error: {ex.Message}");
                                    return "/server_error";
                                }

                                AllClients.Add(user);
                                if (clientSocket != null && !string.IsNullOrEmpty(user.Token))
                                {
                                    ClientSockets[user.Token] = clientSocket;
                                }

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"New client connection: {user.Login} - {user.Token}");

                                return $"/token:{user.Token}";
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"There is not enough space on the license server");
                                return "/limit";
                            }
                        }
                        else
                        {
                            return "/invalid_password";
                        }
                    }
                    else
                    {
                        return "/user_not_found";
                    }
                }
                return "/invalid_format";
            }
            else
            {
                Classes.Client client = AllClients.Find(x => x.Token == command);
                return client != null ? "/connect" : "/disconnect";
            }
        }

        static string GenerateToken()
        {
            Random random = new Random();
            string Chars = "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm0123456789";
            return new string(Enumerable.Repeat(Chars, 20).Select(x => x[random.Next(Chars.Length)]).ToArray());
        }

        static void ConnectServer()
        {
            IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
            Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(EndPoint);
            socketListener.Listen(10);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Server started on {serverIpAdress}:{ServerPort}");
            Console.WriteLine("Waiting for connections...");

            while (true)
            {
                Socket clientSocket = null;
                try
                {
                    clientSocket = socketListener.Accept();
                    string clientIp = ((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString();

                    Thread clientThread = new Thread(() => HandleClient(clientSocket, clientIp));
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error accepting connection: {ex.Message}");
                    clientSocket?.Close();
                }
            }
        }

        static void HandleClient(Socket clientSocket, string clientIp)
        {
            try
            {
                byte[] Bytes = new byte[10485760];
                int ByteRec = clientSocket.Receive(Bytes);

                if (ByteRec == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Client {clientIp} disconnected without sending data");
                    clientSocket.Close();
                    return;
                }

                string Message = Encoding.UTF8.GetString(Bytes, 0, ByteRec);
                string Response = SetCommandClient(Message, clientSocket);

                clientSocket.Send(Encoding.UTF8.GetBytes(Response));

                if (Response == "/disconnect")
                {
                    clientSocket.Close();
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Client {clientIp} forcibly closed the connection");

                var clientToken = ClientSockets.FirstOrDefault(x =>
                    ((IPEndPoint)x.Value.RemoteEndPoint).Address.ToString() == clientIp).Key;

                if (!string.IsNullOrEmpty(clientToken))
                {
                    var client = AllClients.Find(c => c.Token == clientToken);
                    if (client != null)
                    {
                        DisconnectClient(client, "connection reset by client");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error handling client {clientIp}: {ex.Message}");
            }
            finally
            {
                try { clientSocket.Close(); } catch { }
            }
        }
    }
}