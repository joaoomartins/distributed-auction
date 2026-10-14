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
                    else if (response == "ADMISSION_CLOSED")
                    {
                        Console.WriteLine("[Client] The admission window has already closed. You cannot join this auction.");
                        Console.WriteLine("Press any key to close...");
                        Console.ReadKey();
                        return; //shutdown client   
                    }
                    else if (response == null)
                    {
                        Console.WriteLine("\n[Client] Server closed the connection.");
                        Console.WriteLine("Press any key to close...");
                        Console.ReadKey();
                        return;
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
                    Console.WriteLine("Commands available:");
                    Console.WriteLine("  OFFER <value>  -> e.g. OFFER 250");
                    Console.WriteLine("  STATE        -> Check current auction status");
                    Console.WriteLine("  Exit         -> Leave the auction");
                    Console.WriteLine("==============================================\n");
                }

                //communication
                while (true) 
                {
                    Console.WriteLine("Command > ");
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

                    //auction ended
                    if (response.StartsWith("AUCTION_ENDED;"))
                    {
                        string[] endedResponse = response.Split(';');
                        Console.WriteLine("\n***************************************************");
                        Console.WriteLine("***             THE AUCTION HAS ENDED!          ***");
                        if (endedResponse.Length > 1 && endedResponse[1] == "WINNER")
                        {
                            string offererWinner = endedResponse.Length > 2 ? endedResponse[2] : "None";
                            string offerWinner = endedResponse.Length > 3 ? endedResponse[3] : "0";
                            Console.WriteLine($"Result: WINNER is '{offererWinner}' with bid of {offerWinner} EUR!");
                        }
                        else
                        {
                            Console.WriteLine("Result: Item was NOT sold (no valid bids above base price).");
                        }
                        Console.WriteLine("Auction is closed. Please type 'Exit' to leave.");
                        Console.WriteLine("***************************************************\n");
                    }
                    //command OFFER ok
                    else if(response.StartsWith("OFFER_OK;"))
                    {
                        string[] successOfferResponse = response.Split(';');
                        Console.WriteLine($"[OFFER_SUCESS] Your offer of {successOfferResponse[1]}EUR was accepted!");
                    } else if (response.StartsWith("OFFER_REJECTED;"))
                    {
                        string[] badOfferResponse = response.Split(';');
                        Console.WriteLine($"[OFFER_REJECTED] {badOfferResponse[1]}");
                    } else if (response.StartsWith("STATE_INFO"))
                    {
                        string[] stateResponse = response.Split(';');
                        Console.WriteLine("\n--- CURRENT AUCTION STATE ---");
                        Console.WriteLine($"Item: {stateResponse[1]}");
                        Console.WriteLine($"Base price: {stateResponse[2]} EUR");
                        Console.WriteLine($"Highest offer: {(stateResponse[3] == "0" ? "No offres yet" : stateResponse[3] + " EUR")}");
                        Console.WriteLine($"Current offerer: {stateResponse[4]}");
                        Console.WriteLine("-----------------------------\n");
                    } else
                    {
                        Console.WriteLine("Response: " + response);
                    }

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
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
        }
    }
}