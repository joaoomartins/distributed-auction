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
            Console.WriteLine("--- CONFIGURATION ---");
            Console.WriteLine("Enter the product name: ");
            ITEM_NAME = Console.ReadLine()!;
            while (string.IsNullOrWhiteSpace(ITEM_NAME))
            {
                Console.WriteLine("The product name can´t be empty!");
                Console.WriteLine("Enter the product name: ");
                ITEM_NAME = Console.ReadLine()!;
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

                CancellationTokenSource cts = new CancellationTokenSource(ADMISSION_TIME * 1000);
                while (true)
                {
                    try
                    {
                        TcpClient clientSocket = serverSocket.AcceptTcpClientAsync(cts.Token).GetAwaiter().GetResult();

                        offerersCount++;
                        Console.WriteLine($"Main: Offerer {offerersCount} connected.");

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

                        serverSocket.Stop();

                        Console.WriteLine("[MAIN] Time to accept new players has ended!");
                        Console.WriteLine("");
                        Console.WriteLine("*** THE AUCTION HAS STARTED! ***");
                        Console.WriteLine($"Item: {Server.ITEM_NAME} | Starting base price: {ITEM_BASE_PRICE} EUR|Time remaining: {MAX_TIME}sec.");
                        Console.WriteLine("");

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

                string finalWinner = "";
                double finalHighestOffer = 0;
                bool itemSold = false;

                mutex.WaitOne();
                try
                {
                    AUCTION_ENDED = true;
                    if (HIGHEST_OFFER >= ITEM_BASE_PRICE && !string.IsNullOrEmpty(HIGHEST_OFFERER))
                    {
                        finalWinner = HIGHEST_OFFERER;
                        finalHighestOffer = HIGHEST_OFFER;
                        itemSold = true;
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                Console.WriteLine("[MAIN] Auction time has ended!");

                //RESULTS
                Console.WriteLine("\n=== AUCTION RESULTS ===");
                if (itemSold)
                {
                    Console.WriteLine($"Winner: {finalWinner} with offer of {finalHighestOffer} EUR!");
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