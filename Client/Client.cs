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
            while (string.IsNullOrWhiteSpace(hostname))
            {
                Console.WriteLine("Enter an valid hostname server: ");
                hostname = Console.ReadLine()!;
            }

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

                    //if (string.IsNullOrWhiteSpace(username))
                    //{
                    //    Console.WriteLine("[Client] Username cannot be empty.");
                    //    continue;
                    //}

                    if (username.Contains(';'))
                    {
                        Console.WriteLine("[Client] Username cannot contain semicolons (';'). Try again.");
                        continue;
                    }

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
                    else if (response.StartsWith("INVALID_NAME;"))
                    {
                        string[] invalidResponse = response.Split(';');
                        string errorMsg = invalidResponse.Length > 1 ? invalidResponse[1] : "Invalid username.";
                        Console.WriteLine($"[Client] {errorMsg}");
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

                if (infoMessage == null || !infoMessage.StartsWith("AUCTION_START"))
                {
                    Console.WriteLine("[Client] Server closed the connection before the auction started.");
                    Console.WriteLine("Press any key to close...");
                    Console.ReadKey();
                    return;
                }

                string[] information = infoMessage.Split(';');
                string item = information.Length > 1 ? information[1] : "Default Item"; //condition ? true : false
                string price = information.Length > 2 ? information[2] : "0 - Default price";
                string time = information.Length > 3 ? information[3] : "Unknown time";

                Console.WriteLine("\n==============================================");
                Console.WriteLine("*** THE AUCTION HAS STARTED! ***");
                Console.WriteLine($"Item: {item} | Starting base price: {price} EUR|Time remaining: {time}sec.");
                Console.WriteLine("Commands available:");
                Console.WriteLine("  OFFER <value>  -> e.g. OFFER 250");
                Console.WriteLine("  STATE        -> Check current auction status");
                Console.WriteLine("  Exit         -> Leave the auction");
                Console.WriteLine("==============================================\n");

                //communication
                while (true) 
                {
                    Console.Write("Command > ");
                    string? text = Console.ReadLine();

                    //if ENTER pressed without text
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = "STATE";
                    }

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
                        
                        string offererWinner = endedResponse.Length > 2 ? endedResponse[2] : "None"; //condicao ? true : false
                        string offerWinner = endedResponse.Length > 3 ? endedResponse[3] : "0";
                        
                        if (endedResponse.Length > 1 && endedResponse[1] == "WINNER") 
                        {
                            if (offererWinner == username)
                            {
                                Console.WriteLine($"You are the WINNER with an offer of {offerWinner} EUR! CONGRATULATIONS!!!");
                            } else
                            {
                                Console.WriteLine($"Result: WINNER is '{offererWinner}' with offer of {offerWinner} EUR!");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Result: Item was NOT sold (no valid offers above base price).");
                        }
                        Console.WriteLine("Auction is closed. Please type 'Exit' to leave.");
                        Console.WriteLine("***************************************************\n");
                    }
                    //command OFFER ok
                    else if(response.StartsWith("OFFER_OK;"))
                    {
                        string[] successOfferResponse = response.Split(';');
                        string offerAmountValue = successOfferResponse.Length > 1 ? successOfferResponse[1] : "Unkownon value";
                        string highestValue = successOfferResponse.Length > 2 ? successOfferResponse[2] : offerAmountValue;

                        Console.WriteLine($"[OFFER_SUCCESS] Your offer of {offerAmountValue} EUR was accepted! Current highest: {highestValue} EUR.");
                    } 
                    else if (response.StartsWith("OFFER_REJECTED;"))
                    {
                        string[] badOfferResponse = response.Split(';');
                        string reason = badOfferResponse.Length > 1 ? badOfferResponse[1] : "Offer rejected";
                        string currentHighest = badOfferResponse.Length > 2 ? badOfferResponse[2] : "0";

                        Console.WriteLine($"[OFFER_REJECTED] {reason} | Current highest: {currentHighest} EUR");
                    } 
                    else if (response.StartsWith("STATE_INFO"))
                    {
                        string[] stateResponse = response.Split(';');
                        string stateItem = stateResponse.Length > 1 ? stateResponse[1] : "Unknown item";
                        string basePrice = stateResponse.Length > 2 ? stateResponse[2] : "Unknown base price";
                        string highestOffer = stateResponse.Length > 3 ? stateResponse[3] : "Unknown highest price";
                        string highestOfferer = stateResponse.Length > 4 ? stateResponse[4] : "Unknown highest offerer";

                        Console.WriteLine("\n--- CURRENT AUCTION STATE ---");
                        Console.WriteLine($"Item: {stateItem}");
                        Console.WriteLine($"Base price: {basePrice} EUR");
                        Console.WriteLine($"Highest offer: {(highestOffer == "0" ? "No offers yet" : highestOffer + " EUR")}");
                        Console.WriteLine($"Current offerer: {highestOfferer}");
                        Console.WriteLine("-----------------------------\n");
                    } else
                    {
                        Console.WriteLine("Server: " + response);
                    }

                }
            }
            catch (SocketException)
            {
                Console.Error.WriteLine($"[Client] Connection refused or lost to {hostname}:{portNumber}. (Admission may have closed).");
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