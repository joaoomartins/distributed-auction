using Shared;
using System.Net;
using System.Net.Sockets;

namespace LeilaoDistribuido.Server
{
    public class Server
    {

        //shared data
        public static string ITEM_NAME = "";
        public static double ITEM_BASE_PRICE;
        public static int MAX_TIME;

        //users and offers
        public static double HIGHEST_OFFER = 0;
        public static string HIGHEST_OFFERER = "";
        public static readonly List<string> USERNAMES = new List<string>();

        //synchronization
        public static readonly Mutex mutex = new Mutex();
        public static readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        public static void Main()
        {

            Console.WriteLine("--- CONFIGURATION ---");
            Console.WriteLine("Enter the product name: ");
            ITEM_NAME = Console.ReadLine();
            while (string.IsNullOrWhiteSpace(ITEM_NAME))
            {
                Console.WriteLine("The product name can´t be empty!");
                ITEM_NAME = Console.ReadLine();
            }

            ITEM_BASE_PRICE = InputValidation.ValidateDoubleGT0("Enter the product base price (EUR): ");
            MAX_TIME = InputValidation.ValidateIntGT0("Enter the max duration (sec): ");
            int portNumber = InputValidation.ValidateIntBetween("Enter the port number: ", 1024, 65535);

            //get hostname
            string hostname = Dns.GetHostName();
            TcpListener? serverSocket = null;
            int clientCount = 0;

            try
            {
                serverSocket = new TcpListener(IPAddress.Any, portNumber);
                serverSocket.Start();

                Console.WriteLine($"[Server] Auction of '{ITEM_NAME}' | Base price: {ITEM_BASE_PRICE} EUR | Duration: {MAX_TIME}s.");
                Console.WriteLine($"[Server] Listening on {hostname}:{portNumber}");
                Console.WriteLine("[Server]: waiting offerers...");

                while (true)
                {
                    //accept tcp
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();
                    clientCount++;

                    Console.WriteLine("Main: new offerer connected...");

                    //create and start a unique client thread
                    ServerThread serverThread = new ServerThread(clientSocket);
                    
                    Thread thread = new Thread(serverThread.Run);
                    thread.Name = "Server thread-" + clientCount;

                    //thrads.Add
                    thread.Start();
                }
            }
            catch (SocketException)
            {
                Console.Error.WriteLine("Main: An I/O error ocurred while trying to create the socket port " + portNumber);
                Environment.Exit(1);
            }
            finally
            {
                serverSocket?.Stop();
            }
        }
    }
}