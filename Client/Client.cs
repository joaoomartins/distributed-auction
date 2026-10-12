using Shared;
using System.Net.Sockets;
using static System.Net.Mime.MediaTypeNames;

namespace LeilaoDistribuido.Client
{
    public class Client
    {
        public static void Main()
        {
            Console.WriteLine("Enter hostname server: ");
            string hostname = Console.ReadLine()!;
            int portNumber = InputValidation.ValidateIntBetween("Enter the port number: ", 1024, 65535);


            try
            {
                using TcpClient clientSocket = new TcpClient(hostname, portNumber);
                NetworkStream stream = clientSocket.GetStream();
                StreamWriter outWriter = new StreamWriter(stream) { AutoFlush = true };
                StreamReader inReader = new StreamReader(stream);

                Console.WriteLine("[Client] Connected to server!");

                //validate name

                string username;

                while (true)
                {
                    Console.WriteLine("Enter your username: ");
                    username = Console.ReadLine();

                    outWriter.WriteLine(username);
                    string? response = inReader.ReadLine();

                    if (response == "OK")
                    {
                        Console.WriteLine($"[Client] Welcome, {username}! Successfully registered.");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"[Client] The username {username} already exists. Try other username.");
                    } 
                }

                Console.WriteLine("You can send test messages.Type “Exit” to leave");

                //conversation
                while (true) 
                {
                    Console.WriteLine("Message: ");
                    string? text = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    outWriter.WriteLine(text);

                    if (text.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Turnning off...");
                        break;
                    }

                    string? response = inReader.ReadLine();
                    Console.WriteLine("Response: " + response);
                }
            }
            catch (SocketException)
            {
                Console.Error.WriteLine("Unknown host or connection refused: " + hostname);
                Environment.Exit(2);
            }
            catch (IOException)
            {
                Console.Error.WriteLine("I/O error");
                Environment.Exit(3);
            }
        }
    }
}