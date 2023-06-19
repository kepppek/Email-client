using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Mail
{
    public class Client
    {
        public Socket socket;
        public SslStream sslStream;
        public Dictionary<string, object> headers;
        public List<string> data;
        public string message;
        public byte[] byteMessage;
        ListBox lb;
        public int count;

        public void GetCountMail()
        {
            SendCommand("STAT");

            string [] s = lb.Items[lb.Items.Count-1].ToString().Split(' ');
            count = int.Parse(s[1]);
        }
        public void Console(ListBox LB)
        {
            this.lb = LB;
        }
        static bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
        public void Delete(int number)
        {
            SendCommand("DELE " + number);
        }
        public void RunClient(string hostname, int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(hostname, port);
            socket.ReceiveTimeout = 60000;
            socket.SendTimeout = 60000;
             lb.Items.Add("Client connected.");
            Stream s = new NetworkStream(socket);

            sslStream = new SslStream(s, false,
               new RemoteCertificateValidationCallback(CertificateValidationCallback), null);

            sslStream.ReadTimeout = 60000;
            sslStream.WriteTimeout = 60000;
            sslStream.AuthenticateAsClient(hostname);
            Stream stream = (Stream)sslStream;

            string response = ReadLineAsAscii(stream);
             lb.Items.Add(response);
        }

        /// <summary>
        /// Закрытие потока
        /// </summary>
        public void Close()
        {
            SendCommand("QUIT");
            socket.Close();
        }

        /// <summary>
        /// Авторизация
        /// </summary>
        /// <param name="password"></param>
        /// <param name="login"></param>
        public void AuthorizationPOP(string password, string login)
        {
            SendCommand("USER " + login);
            SendCommand("PASS " + password);
        }

        /// <summary>
        /// Авторизация
        /// </summary>
        /// <param name="password"></param>
        /// <param name="login"></param>
        public void AuthorizationSMTP(string password, string login)
        {
            SendCommand("HELO " + login);
            SendCommand("AUTH LOGIN");
            SendCommand(Convert.ToBase64String(Encoding.UTF8.GetBytes(login)));
            SendCommand(Convert.ToBase64String(Encoding.UTF8.GetBytes(password)));
        }

        /// <summary>
        /// Отправить метод
        /// </summary>
        /// <param name="command"></param>
        public void SendCommand(string command)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(command + "\r\n");
             lb.Items.Add(string.Format("SendCommand: \"{0}\"", (object)command));
            sslStream.Write(bytes, 0, bytes.Length);
            sslStream.Flush();
             lb.Items.Add(ReadLineAsAscii(sslStream));
        }

        /// <summary>
        /// Помогает считывать овтет сервера
        /// </summary>
        /// <param name="bytesReceived"></param>
        /// <returns></returns>
        private static bool IsLastLineInMultiLineResponse(byte[] bytesReceived)
        {
            if (bytesReceived == null)
                throw new ArgumentNullException(nameof(bytesReceived));
            if (bytesReceived.Length == 1)
                return bytesReceived[0] == (byte)46;
            return false;
        }

        /// <summary>
        /// Считывает всё сообщение от сервера
        /// </summary>
        /// <param name="messageNumber"></param>
        /// <param name="askOnlyForHeaders"></param>
        /// <returns></returns>
        public void GetMessageAsBytes(int messageNumber, bool askOnlyForHeaders)
        {
            if (askOnlyForHeaders)
                this.SendCommand("TOP " + (object)messageNumber + " 0");
            else
                this.SendCommand("RETR " + (object)messageNumber);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bool flag = true;
                byte[] buffer;
                while (!IsLastLineInMultiLineResponse(buffer = ReadLineAsBytes(sslStream)))
                {
                    if (!flag)
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes("\r\n");
                        memoryStream.Write(bytes, 0, bytes.Length);
                    }
                    else
                        flag = false;
                    if (buffer.Length > 0 && buffer[0] == (byte)46)
                        memoryStream.Write(buffer, 1, buffer.Length - 1);
                    else
                        memoryStream.Write(buffer, 0, buffer.Length);
                }
                if (askOnlyForHeaders)
                {
                    byte[] bytes = Encoding.ASCII.GetBytes("\r\n");
                    memoryStream.Write(bytes, 0, bytes.Length);
                }
                byteMessage = memoryStream.ToArray();
                message = Encoding.UTF8.GetString(byteMessage);
            }
        }

        /// <summary>
        /// Считывает все байты из ответа сервера
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static byte[] ReadLineAsBytes(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            using (MemoryStream memoryStream = new MemoryStream())
            {
                char ch;
                do
                {
                    int num = stream.ReadByte();
                    if (num != -1 || memoryStream.Length <= 0L)
                    {
                        if (num == -1 && memoryStream.Length == 0L)
                            return (byte[])null;
                        ch = (char)num;
                        switch (ch)
                        {
                            case '\n':
                            case '\r':
                                continue;
                            default:
                                memoryStream.WriteByte((byte)num);
                                goto case '\n';
                        }
                    }
                    else
                        break;
                }
                while (ch != '\n');
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Считывает первую строчку ответа сервера
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static string ReadLineAsAscii(Stream stream)
        {
            byte[] bytes = ReadLineAsBytes(stream);
            if (bytes == null)
                return (string)null;
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Функция парсит заголовки и возвращает коллекцию Dictionary
        /// </summary>
        /// <param name="h">Источник, из которого нужно получить заголовки</param>
        public void GetHeaders()
        {
            int headersTail = message.IndexOf("\r\n\r\n"); // еще пригодится

            string s = message.Substring(0, headersTail);
            headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            // декодируем текстовые данные в заголовках
            s = Regex.Replace(s, @"([\x22]{0,1})\=\?(?<cp>[\w\d\-]+)\?(?<ct>[\w]{1})\?(?<value>[^\x3f]+)\?\=([\x22]{0,1})", HeadersEncode, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            // удаляем лишные пробелы
            s = Regex.Replace(s, @"([\r\n]+)^(\s+)(.*)?$", " $3", RegexOptions.Multiline);
            // а теперь парсим заголовки и заносим их в коллекцию
            Regex myReg = new Regex(@"^(?<key>[^\x3A]+)\:\s{1}(?<value>.+)$", RegexOptions.Multiline);
            MatchCollection mc = myReg.Matches(s);
            foreach (Match m in mc)
            {
                string key = m.Groups["key"].Value;
                if (headers.ContainsKey(key))
                {
                    // если указанный ключ уже есть в коллекции,
                    // то проверяем тип данных
                    if (headers[key].GetType() == typeof(string))
                    {
                        // тип данных - строка, преобразуем в коллекцию
                        ArrayList arr = new ArrayList();
                        // добавляем в коллекцию первый элемент
                        arr.Add(headers[key]);
                        // добавляем в коллекцию текущий элемент
                        arr.Add(m.Groups["value"].Value);
                        // вставляем коллекцию элементов в найденный заголовок
                        headers[key] = arr;
                    }
                    else
                    {
                        // считаем, что тип данных - коллекция, 
                        // добавляем найденный элемент
                        ((ArrayList)headers[key]).Add(m.Groups["value"].Value);
                    }
                }
                else
                {
                    // такого ключа нет, добавляем
                    headers.Add(key, m.Groups["value"].Value.TrimEnd("\r\n ".ToCharArray()));
                }
            }
            // возвращаем коллекцию полученных заголовков
        }

        /// <summary>
        /// Функция обратного вызова, обрабатывается в методе ParseHeaders, производит декодирование данных в заголовках, в соответствии с найденными атрибутами.
        /// </summary>
        private static string HeadersEncode(Match m)
        {
            string result = String.Empty;
            Encoding cp = Encoding.GetEncoding(m.Groups["cp"].Value);
            if (m.Groups["ct"].Value.ToUpper() == "B")
            {
                // кодируем из Base64
                result = cp.GetString(Convert.FromBase64String(m.Groups["value"].Value));
            }
            else
            {
                // такого быть не должно, оставляем текст как есть
                result = m.Groups["value"].Value;
            }
            return result; //ConvertCodePage(result, cp);
        }

        /// <summary>
        /// Парсит часть data и возвращает список текстов
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public void GetData()
        {
            int headersTail = message.IndexOf("\r\n\r\n"); // еще пригодится

            string s = message.Substring(headersTail + 4, message.Length - headersTail - 4);

            data = new List<string>();
            for (int i = 0; i < s.Length; i++)
            {
                i = s.IndexOf("Content-Transfer-Encoding: base64", i);

                if (i == -1)
                {
                    if(data.Count==0)
                    data.Add(s);
                    break;
                }
                else
                {
                    i += 37;
                }

                string s2 = "";
                while (i != s.Length - 1)
                {
                    if (s[i] == '-')
                    {
                        i++;
                        break;
                    }
                    if (s[i] == '\r' || s[i] == '\n')
                    {
                        i++;
                        continue;
                    }
                    s2 += s[i];
                    i++;
                }

                if(s2.Length%2==0)
                data.Add(Encoding.UTF8.GetString(Convert.FromBase64String(s2)));
             
            }
        }
    }
}
