using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;

namespace LeilaoDistribuido.Server
{
    internal class ServerThread
    {
        private readonly TcpClient clientSocket;
        private string username = string.Empty;

        public ServerThread(TcpClient clientSocket)
        {
            this.clientSocket = clientSocket;
        }

        public void Run()
        {
            try
            {
                NetworkStream stream = clientSocket.GetStream();
                StreamWriter outWriter = new StreamWriter(stream) { AutoFlush = true };
                StreamReader inReader = new StreamReader(stream);

                //user regist | verify duplicated name | mutex
                while (true)
                {
                    string? attemptUsername = inReader.ReadLine();
                    if (attemptUsername == null) return;

                    //regist after admission time
                    if (!Server.ADMISSION_OPEN)
                    {
                        outWriter.WriteLine("ADMISSION_CLOSED");
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": Refused (admission window closed).");
                        return; // Termina e fecha socket no bloco finally
                    }

                    bool acepted = false;

                    Server.mutex.WaitOne();

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(attemptUsername) && !Server.USERNAMES.Contains(attemptUsername))
                        {
                            Server.USERNAMES.Add(attemptUsername);
                            username = attemptUsername;
                            acepted = true;
                        }
                    }
                    finally
                    {
                        Server.mutex.ReleaseMutex();
                    }

                    if (acepted)
                    {
                        outWriter.WriteLine("NAME_OK");
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": User registed -> " + username);
                        break;
                    }
                    else
                    {
                        outWriter.WriteLine("Duplicated name");
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": Refused name -> " + attemptUsername);
                    }
                }

                //wait for admission time
                Console.WriteLine("Thread " + Thread.CurrentThread.Name + " (" + username + "): Waiting for admission window to close...");

                Server.semaphore.Wait();

                //send info to offerer
                outWriter.WriteLine($"AUCTION_START;{Server.ITEM_NAME};{Server.ITEM_BASE_PRICE}");
                Console.WriteLine("Thread " + Thread.CurrentThread.Name + " (" + username + "): Auction started for this user.");

                //notify the offerer that the auction has begun
                while (true)
                {
                    string? message = inReader.ReadLine();

                    if (message == null || message.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": User " + username + " left!");
                        break;
                    }

                    if (Server.AUCTION_ENDED)
                    {
                        Server.mutex.WaitOne();
                        try
                        {
                            if(Server.HIGHEST_OFFER >= Server.ITEM_BASE_PRICE && !string.IsNullOrEmpty(Server.HIGHEST_OFFERER))
                            {
                                outWriter.WriteLine($"AUCTION_ENDED;WINNER;{Server.HIGHEST_OFFERER};{Server.HIGHEST_OFFER}");
                            }
                            else
                            {
                                outWriter.WriteLine("AUCTION_ENDED;NO_WINNER;0");
                            }
                        }
                        finally
                        {
                            Server.mutex.ReleaseMutex();
                        }

                        Console.WriteLine($"Thread {Thread.CurrentThread.Name} ({username}): Notified of auction end.");
                        continue;
                    }

                    if (message.Equals("STATE", StringComparison.OrdinalIgnoreCase))
                    {
                        Server.mutex.WaitOne();
                        try
                        {
                            string topOfferer = string.IsNullOrEmpty(Server.HIGHEST_OFFERER) ? "None" : Server.HIGHEST_OFFERER;
                            outWriter.WriteLine($"STATE_INFO;{Server.ITEM_NAME};{Server.ITEM_BASE_PRICE};{Server.HIGHEST_OFFER};{topOfferer}");
                        } 
                        finally
                        {
                            Server.mutex.ReleaseMutex();
                        }
                        continue;
                    }

                    //command OFFER: when offerer submit a offer
                    if (message.StartsWith("OFFER", StringComparison.OrdinalIgnoreCase)) {
                        string[] offerMessage = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        //
                        if (offerMessage.Length < 2 || !double.TryParse(offerMessage[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double offerAmount))
                        {
                            outWriter.WriteLine("OFFER_REJECTED;Invalid format. Use OFFER <amount>");
                            Console.WriteLine($"Thread {Thread.CurrentThread.Name} ({username}): Sent wrong format offer '{message}'");
                            continue;
                        }

                        Server.mutex.WaitOne();
                        try
                        {
                            if (Server.AUCTION_ENDED)
                            {
                                outWriter.WriteLine("OFFER_REJECTED;Auction has already ended.");
                                Console.WriteLine($"Thread {Thread.CurrentThread.Name} ({username}): Offer rejected (auction ended).");
                            } else if (offerAmount < Server.ITEM_BASE_PRICE)
                            {
                                outWriter.WriteLine($"OFFER_REJECTED;Offer must be at least the base price of {Server.ITEM_BASE_PRICE} EUR");
                                Console.WriteLine($"Thread {Thread.CurrentThread.Name}({username}): Offerer rejected (Auction ended).");
                            } else if (offerAmount <= Server.HIGHEST_OFFER)
                            {
                                outWriter.WriteLine($"OFFER_REJECTED; Offer must be higher than current highest offer of {Server.HIGHEST_OFFER} EUR.");
                                Console.WriteLine($"Thread {Thread.CurrentThread.Name} ({username}): Bid of {offerAmount} EUR rejected (Not higher than {Server.HIGHEST_OFFER} EUR).");
                            } else
                            {
                                //update status
                                Server.HIGHEST_OFFER = offerAmount;
                                Server.HIGHEST_OFFERER = username;

                                outWriter.WriteLine($"OFFER_OK;{offerAmount}");
                                Console.WriteLine($"Thread {Thread.CurrentThread.Name} ({username}): Offer of {offerAmount}EUR accepted. New highest offer!");
                            }
                        }
                        finally
                        {
                            Server.mutex.ReleaseMutex();
                        }

                        continue;
                    }

                    //unknow command
                    outWriter.WriteLine("UNKNOWN_COMMAND;Supported commands: OFFER <amount>, STATE, Exit\"");
                }
            }
            catch (IOException)
            {
                Console.Error.WriteLine("Thread " + Thread.CurrentThread.Name + ": Connection lost with " + username);
            }
            finally
            {
                if (!string.IsNullOrEmpty(username))
                {
                    Server.mutex.WaitOne();
                    try
                    {
                        Server.USERNAMES.Remove(username);
                    }
                    finally
                    {
                        Server.mutex.ReleaseMutex();
                    }
                }

                clientSocket.Close();
                Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": Socket closed.");
            }
        }
    }
}