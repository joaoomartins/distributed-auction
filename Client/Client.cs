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

                    if (response == "NAME_OK")
                    {
                        Console.WriteLine($"[Client] Welcome, {username}! Successfully registered.");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"[Client] The username {username} already exists. Try other username.");
                    } 
                }

                //waiting for the auction to begin
                Console.WriteLine("\n[Client] Waiting for the admission window to close and auction to start...");

                string? infoMessage = inReader.ReadLine();
                //MESSAGE RECEIVED: AUCTION_START: ITEM_NAME;ITEM_BASE_PRICE index 0 1 2

                if (infoMessage != null && infoMessage.StartsWith("AUCTION_START"))
                {
                    string[] information = infoMessage.Split(';');
                    string item = information.Length > 1 ? information[1] : "Default Item"; //condition ? true : false
                    string price = information.Length > 2 ? information[2] : "0 - Default price";

                    Console.WriteLine("\n==============================================");
                    Console.WriteLine("*** THE AUCTION HAS STARTED! ***");
                    Console.WriteLine($"Item: {item} | Starting base price: {price} EUR");
                    Console.WriteLine("You can send messages/bids. Type 'Exit' to leave.");
                    Console.WriteLine("==============================================\n");
                }


                //communication
                while (true) 
                {
                    Console.WriteLine("Message: ");
                    string? text = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    outWriter.WriteLine(text);

                    if (text.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Leaving the auction...");
                        break;
                    }

                    string? response = inReader.ReadLine();
                    if (response == null)
                    {
                        Console.WriteLine("[Client] Server closed the connection.");
                        break;
                    }

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