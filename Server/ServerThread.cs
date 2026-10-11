using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace LeilaoDistribuido.Server
{
    internal class ServerThread
    {
        private readonly TcpClient clientSocket;

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

                while (true)
                {
                    string? message = inReader.ReadLine();

                    if (message.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": The client left the conversation.");
                        break;
                    }

                    Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": The client sent: " + message);

                    outWriter.WriteLine("Server received: " + message);
                }
            } catch (IOException)
            {
                Console.Error.WriteLine("Thread " + Thread.CurrentThread.Name + ": Error creating the socket buffers");
            }
            finally
            {
                clientSocket.Close();
                Console.WriteLine("Thread " + Thread.CurrentThread.Name + ": Client disconnected and socket closed.");
            }
        }
    }
}