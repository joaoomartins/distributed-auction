using Shared;
using System.Net.Sockets;

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

                Console.WriteLine("Connected to server! Type 'Exit' to quit.");

                while (true)
                {
                    Console.WriteLine("Enter a message to send to the server: ");
                    string text = Console.ReadLine();

                    outWriter.WriteLine(text);

                    if (text.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Exiting client application...");
                        break;
                    }

                    string? response = inReader.ReadLine();
                    Console.WriteLine("Server response: " + response);
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