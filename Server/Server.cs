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
        public static int ADMISSION_TIME;

        public static volatile bool ADMISSION_OPEN = true;
        public static volatile bool AUCTION_ENDED = false;

        //users and offers
        public static double HIGHEST_OFFER = 0;
        public static string HIGHEST_OFFERER = "";
        public static readonly List<string> USERNAMES = new List<string>();

        //synchronization
        public static readonly Mutex mutex = new Mutex();
        public static readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        public static void Main()
        {
            int nUsers = 0;

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
            ADMISSION_TIME = InputValidation.ValidateIntGT0("Enter the admission time window (sec): ");
            int portNumber = InputValidation.ValidateIntBetween("Enter the port number: ", 1024, 65535);

            //get hostname
            string hostname = Dns.GetHostName();
            TcpListener? serverSocket = null;
            List<Thread> threads = new List<Thread>();
            int offerersCount = 0;

            try
            {
                serverSocket = new TcpListener(IPAddress.Any, portNumber);
                serverSocket.Start();

                Console.WriteLine($"[Server] Auction of '{ITEM_NAME}' | Base price: {ITEM_BASE_PRICE} EUR | Duration: {MAX_TIME}s.");
                Console.WriteLine($"[Server] Listening on {hostname}:{portNumber}");
                Console.WriteLine($"[Server] Admission window OPEN for {ADMISSION_TIME} seconds. Waiting offerers...\n");

                while (true)
                {
                    Console.WriteLine("Main: new offerer connected...");
                    
                    try
                    {
                        CancellationTokenSource cts = new CancellationTokenSource(ADMISSION_TIME * 1000);
                        TcpClient clientSocket = serverSocket.AcceptTcpClientAsync(cts.Token).GetAwaiter().GetResult();

                        Console.WriteLine("Main: new connection...");

                        offerersCount++;

                        //create and start a unique client thread
                        ServerThread serverThread = new ServerThread(clientSocket);
                        Thread thread = new Thread(serverThread.Run)
                        {
                            Name = "Server thread-" + offerersCount
                        };

                        threads.Add(thread);
                        thread.Start();
                    }
                    catch (OperationCanceledException)
                    {
                        ADMISSION_OPEN = false;
                        Console.WriteLine("[MAIN] Time to accept new players has ended!");
                    
                        if (offerersCount == 0)
                        {
                            Console.WriteLine("[MAIN] No offerers joined. Auction cancelled.");
                            return;
                        }

                        semaphore.Release(offerersCount);
                        break;
                    }
                }

                //auction timer
                DateTime startAuction = DateTime.Now;
                foreach (Thread thread in threads)
                {
                    TimeSpan remaining = TimeSpan.FromSeconds(MAX_TIME) - (DateTime.Now - startAuction);
                    if (remaining.TotalMilliseconds > 0)
                    {
                        thread.Join(remaining);
                    }
                }

                AUCTION_ENDED = true;
                Console.WriteLine("[MAIN] Auction time has ended!");

                //RESULTS
                Console.WriteLine("\n=== AUCTION RESULTS ===");
                if (HIGHEST_OFFER >= ITEM_BASE_PRICE && !string.IsNullOrEmpty(HIGHEST_OFFERER))
                {
                    Console.WriteLine($"Winner: {HIGHEST_OFFERER} with offer of {HIGHEST_OFFER} EUR!");
                }
                else
                {
                    Console.WriteLine("Item was not sold (no valid offers above base price).");
                }

                //wait offerers to end program
                Console.WriteLine("[MAIN] Waiting for clients to exit (they must type 'Exit')...");
                foreach (Thread thread in threads)
                {
                    thread.Join();
                }

                Console.WriteLine("[MAIN] All client threads finished. Server shutting down.");

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