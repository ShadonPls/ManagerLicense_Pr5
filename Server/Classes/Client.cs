using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Classes
{
    public class Client
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public DateTime DateConnect { get; set; }
        public bool IsBlacklisted { get; set; }

        public Client()
        {
            // Создание экземпляра класса Random для генерации случайных чисел
            Random random = new Random();

            // Определение строки, содержащей допустимые символы для генерации токена
            string Chars = "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm0123456789";

            // Генерация токена длиной 15 символов:
            // Enumerable.Repeat(Chars, 15) - создает последовательность из 15 копий строки Chars
            Token = new string(Enumerable.Repeat(Chars, 15).Select(x => x[random.Next(Chars.Length)]).ToArray());

            // DateTime.Now - возвращает текущую дату и время с учетом локальных настроек системы
            DateConnect = DateTime.Now;

            // Перезапись даты подключения на текущее время в формате UTC (Всемирное координированное время)
            DateConnect = DateTime.UtcNow;


            // При создании нового клиента он по умолчанию не находится в черном списке
            // Это позволяет администратору вручную добавлять клиентов в черный список при необходимости
            IsBlacklisted = false;
        }
    }
}


