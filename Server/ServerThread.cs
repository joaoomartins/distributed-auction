using System;
using System.Collections.Generic;
using System.Net.Sockets;
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
                        outWriter.WriteLine("OK");
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": User registed -> " + username);
                        break;
                    }
                    else
                    {
                        outWriter.WriteLine("Duplicated name");
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": Refused name -> " + attemptUsername);
                    }
                }

                //mensages
                while (true)
                {
                    string? message = inReader.ReadLine();

                    if (message == null || message.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": User " + username + " left.");
                        break;
                    }

                    Console.WriteLine("Thread " + Thread.CurrentThread.Name + " (" + username + "): " + message);
                    outWriter.WriteLine("Server received " + message + " from " + username);
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