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
        // Статические переменные для хранения данных подключения
        static IPAddress serverIpAdress;  // IP-адрес сервера
        static int ServerPort;            // Порт сервера

        static string ClientToken;        // Токен аутентификации клиента
        static DateTime ClientDateConnection; // Дата и время подключения
        static string ClientLogin;        // Логин клиента

        // Главный метод - точка входа в программу
        static void Main(string[] args)
        {
            OnSettings(); // Загружаем или запрашиваем настройки подключения

            // Создаем и запускаем фоновый поток для проверки токена
            Thread tCheckToken = new Thread(CheckToken);
            tCheckToken.Start();

            // Бесконечный цикл для приема команд от пользователя
            while (true)
            {
                SetCommand();
            }
        }

        // Метод для обработки введенных пользователем команд
        static void SetCommand()
        {
            Console.ForegroundColor = ConsoleColor.Red; // Устанавливаем цвет текста
            string Command = Console.ReadLine(); // Читаем команду из консоли

            // Проверяем введенную команду
            if (Command == "/config")
            {
                // Команда сброса конфигурации
                File.Delete(Directory.GetCurrentDirectory() + "/config"); // Удаляем файл конфигурации
                OnSettings(); // Запрашиваем новые настройки
            }
            else if (Command.StartsWith("/connect "))
            {
                // Команда подключения с аутентификацией
                ConnectServerWithAuth(Command);
            }
            else if (Command == "/status")
            {
                // Команда отображения статуса подключения
                GetStatus();
            }
            else if (Command == "/help")
            {
                // Команда вывода справки
                Help();
            }
            else
            {
                // Неизвестная команда
                Console.WriteLine("Неизвестная команда. Введите /help для списка команд.");
            }
        }

        // Метод для подключения к серверу с аутентификацией
        static void ConnectServerWithAuth(string command)
        {
            // Разбиваем команду на части: /connect логин пароль
            string[] parts = command.Split(' ');
            if (parts.Length >= 3) // Проверяем, что есть логин и пароль
            {
                ClientLogin = parts[1]; // Сохраняем логин
                string password = parts[2]; // Получаем пароль

                // Создаем конечную точку подключения
                IPEndPoint EndPoint = new IPEndPoint(serverIpAdress, ServerPort);
                // Создаем сокет для TCP-подключения
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    socket.Connect(EndPoint); // Пытаемся подключиться к серверу
                }
                catch (Exception ex)
                {
                    // Обработка ошибки подключения
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                    return;
                }

                // Если подключение успешно
                if (socket.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connection to server successful");

                    // Отправляем команду аутентификации на сервер
                    socket.Send(Encoding.UTF8.GetBytes($"/connect {ClientLogin} {password}"));

                    // Буфер для получения ответа от сервера (10 МБ)
                    byte[] Bytes = new byte[10485760];
                    int ByteRec = socket.Receive(Bytes); // Получаем ответ

                    // Преобразуем байты в строку
                    string Response = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                    // Обрабатываем ответы сервера
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
                        // Успешная аутентификация - получаем токен
                        ClientToken = Response.Substring(7); // Извлекаем токен (убираем "/token:")
                        ClientDateConnection = DateTime.Now; // Запоминаем время подключения
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Authentication successful. Received token: {ClientToken}");
                    }
                    else
                    {
                        // Неизвестный ответ от сервера
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Server response: {Response}");
                    }

                    socket.Close(); // Закрываем сокет
                }
            }
            else
            {
                // Неверный формат команды
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Usage: /connect [UserName] [Password]");
            }
        }

        // Метод для подключения без аутентификации (запрашивает токен напрямую)
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

                socket.Send(Encoding.UTF8.GetBytes("/token")); // Запрос токена
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
                    // Получаем токен и сохраняем время подключения
                    ClientToken = Response;
                    ClientDateConnection = DateTime.Now;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Received connection token: " + ClientToken);
                }
            }
        }

        // Метод, выполняющийся в отдельном потоке для проверки токена
        static void CheckToken()
        {
            while (true) // Бесконечный цикл проверки
            {
                // Если токен пустой, ждем 1 секунду и проверяем снова
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

                    socket.Send(Encoding.UTF8.GetBytes(ClientToken)); // Отправляем токен на проверку
                    byte[] Bytes = new byte[10485760];
                    int ByteRec = socket.Receive(Bytes);

                    string Response = Encoding.UTF8.GetString(Bytes, 0, ByteRec);

                    // Если сервер отправил команду отключения
                    if (Response == "/disconnect")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The client is disconnected from the server");
                        ClientToken = String.Empty; // Очищаем токен
                        ClientLogin = String.Empty; // Очищаем логин
                    }
                }
                catch (SocketException)
                {
                    // Обработка недоступности сервера
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Server is unavailable");
                    ClientToken = String.Empty;
                    ClientLogin = String.Empty;
                }
                catch (Exception ex)
                {
                    // Обработка других ошибок
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + ex.Message);
                }
                finally
                {
                    // Гарантированно закрываем сокет
                    try { socket.Close(); } catch { }
                }

                Thread.Sleep(1000); // Пауза между проверками - 1 секунда
            }
        }

        // Метод отображения текущего статуса подключения
        static void GetStatus()
        {
            // Проверяем, есть ли активное подключение
            if (string.IsNullOrEmpty(ClientToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Not connected to server.");
                return;
            }

            // Вычисляем длительность подключения в секундах
            int Duration = (int)DateTime.Now.Subtract(ClientDateConnection).TotalSeconds;

            // Выводим информацию о подключении
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"User: {ClientLogin}, Token: {ClientToken}, " +
                $"Connection time: {ClientDateConnection.ToString("HH:mm:ss dd.MM")}, " +
                $"Duration: {Duration} seconds");
        }

        // Метод загрузки и сохранения настроек подключения
        static void OnSettings()
        {
            string Path = Directory.GetCurrentDirectory() + "/.config"; // Путь к файлу конфигурации
            string IpAdress = ""; // Переменная для хранения IP-адреса

            // Проверяем существование файла конфигурации
            if (File.Exists(Path))
            {
                // Читаем настройки из файла
                StreamReader sr = new StreamReader(Path);
                IpAdress = sr.ReadLine(); // Читаем IP-адрес
                serverIpAdress = IPAddress.Parse(IpAdress); // Парсим IP-адрес
                ServerPort = int.Parse(sr.ReadLine()); // Читаем и парсим порт
                sr.Close(); // Закрываем файл

                // Выводим текущие настройки
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
                // Если файла нет - запрашиваем настройки у пользователя
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please provide the IP address if the license server: ");
                Console.ForegroundColor = ConsoleColor.Green;
                IpAdress = Console.ReadLine(); // Читаем IP-адрес
                serverIpAdress = IPAddress.Parse(IpAdress); // Парсим IP-адрес

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Please specify the license server port: ");
                Console.ForegroundColor = ConsoleColor.Green;
                ServerPort = int.Parse(Console.ReadLine()); // Читаем и парсим порт

                // Сохраняем настройки в файл
                StreamWriter streamWriter = new StreamWriter(Path);
                streamWriter.WriteLine(IpAdress); // Записываем IP-адрес
                streamWriter.WriteLine(ServerPort.ToString()); // Записываем порт
                streamWriter.Close(); // Закрываем файл
            }

            // Информация о команде изменения настроек
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("To change, write the command: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("/config");
        }

        // Метод вывода справки по командам
        static void Help()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Commands to the server: ");

            // Описание команды /config
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/config");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - set initial settings");

            // Описание команды /connect
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/connect [UserName] [Password]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - connect to server with authentication");

            // Описание команды /status
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/status");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show connection status");

            // Описание команды /help аерар
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/help");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - show this help");
        }
    }
} 