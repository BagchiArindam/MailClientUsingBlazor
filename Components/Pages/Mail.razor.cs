using MailFetch1.LIbraries.Constants;
using MailKit;
using MailKit.Net.Imap;
using OpenPop.Mime;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace MailFetch1.Components.Pages
{
    public partial class Mail
    {
        private static string errorMsg = "No Errors";        
        private static int IMAPCount = 0, POP3Count = 0;
        string email = Constants1.email;
        string password = Constants1.password;
        private List<MailEntity>? IMAPInbox;
        private List<MailEntity>? POP3inbox;

        protected override async Task OnInitializedAsync()
        {
            await Task.Delay(500);
            using (var client = new OpenPop.Pop3.Pop3Client())
            {
                client.Connect("pop.gmail.com", 995, true); // Use SSL
                client.Authenticate(email, password);
                int messageCount = client.GetMessageCount();
                POP3Count = messageCount;
                client.Disconnect();
            }
            using (var imapClient = new ImapClient())
            {
                imapClient.Connect("imap.gmail.com", 993, true); // Use SSL
                try
                {
                    imapClient.Authenticate(email, password);
                    var inbox = imapClient.Inbox;
                    inbox.Open(FolderAccess.ReadOnly);
                    IMAPCount = inbox.Count;
                }
                catch (Exception e) { errorMsg = e.Message; }
                imapClient.Disconnect(true);
            }
        }
        private void FetchIMAP()
        {
            IMAPInbox = new();
            using (var imapClient = new ImapClient())
            {
                imapClient.Connect("imap.gmail.com", 993, true); // Use SSL
                try
                {
                    imapClient.Authenticate(email, password);
                    var inbox = imapClient.Inbox;
                    inbox.Open(FolderAccess.ReadOnly);

                    for (int i = 0; i < inbox.Count; i++)
                    {
                        var message = inbox.GetMessage(i);
                        MailEntity entity = new(Convert.ToDateTime(message.Date.DateTime), message.From.ToString(), message.Subject, message.TextBody);
                        IMAPInbox.Add(entity);
                    }
                }
                catch (Exception e) { errorMsg = e.Message; }

                imapClient.Disconnect(true);
            }
        }
        private void FetchPOP3()
        {
            POP3inbox = new();
            using (var client = new OpenPop.Pop3.Pop3Client())
            {
                client.Connect("pop.gmail.com", 995, true); // Use SSL
                client.Authenticate(email, password);
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                for (int i = 1; i <= POP3Count; i++)
                {
                    var message = client.GetMessage(i);
                    MailEntity entity = new(Convert.ToDateTime(message.Headers.DateSent), message.Headers.From.ToString(), message.Headers.Subject, GetBodyText(message));
                    POP3inbox.Add(entity);
                }
                client.Disconnect();
            }
        }
        public class Pop3Client : IDisposable
        {
            private TcpClient tcpClient;
            private SslStream sslStream;
            private StreamReader reader;
            private StreamWriter writer;

            public Pop3Client(string host, int port, bool useSsl)
            {
                tcpClient = new TcpClient(host, port);
                sslStream = new SslStream(tcpClient.GetStream());
                reader = new StreamReader(sslStream, Encoding.ASCII);
                writer = new StreamWriter(sslStream, Encoding.ASCII) { NewLine = "\r\n" };

                if (useSsl)
                {
                    sslStream.AuthenticateAsClient(host);
                }
            }

            public void Authenticate(string username, string password)
            {
                SendCommand($"USER {username}");
                SendCommand($"PASS {password}");
            }

            public int GetMessageCount()
            {
                SendCommand("STAT");
                string response = ReadResponse();
                string[] parts = response.Split(' ');
                return int.Parse(parts[1]);
            }

            public string GetMessage(int messageNumber)
            {
                SendCommand($"RETR {messageNumber}");
                return ReadResponse();
            }

            private void SendCommand(string command)
            {
                writer.WriteLine(command);
                writer.Flush();
            }

            private string ReadResponse()
            {
                string response = "";
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;
                    if (line == ".")
                        break;
                    response += line + "\n";
                }
                return response;
            }

            public void Dispose()
            {
                reader.Dispose();
                writer.Dispose();
                sslStream.Dispose();
                tcpClient.Close();
            }
        }

        private class MailEntity
        {
            public DateTime MailDate { get; set; }
            public string From { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public MailEntity(DateTime _MailDate, string _From, string _Subject, string _Body)
            {
                MailDate = _MailDate;
                From = _From;
                Subject = _Subject;
                Body = _Body;
            }
        }
        private static string GetBodyText(Message message)
        {
            var plainText = message.FindFirstPlainTextVersion();
            var htmlText = message.FindFirstHtmlVersion();

            if (plainText != null)
            {
                return plainText.GetBodyAsText();
            }
            else if (htmlText != null)
            {
                return htmlText.GetBodyAsText();
            }
            else
            {
                return "No text body found.";
            }
        }

    }
}