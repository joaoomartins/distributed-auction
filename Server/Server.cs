using Shared;
using System.Net;
using System.Net.Sockets;

namespace LeilaoDistribuido.Server
{
    public class Server
    {
        public static void Main()
        {
            int portNumber = InputValidation.ValidateIntBetween("Enter the port number: ", 1024, 65535);

            TcpListener? serverSocket = null;
            int clientCount = 0;

            try
            {
                serverSocket = new TcpListener(IPAddress.Any, portNumber);
                serverSocket.Start();

                Console.WriteLine("Main: waiting new connections...");

                while (true)
                {
                    //accept tcp
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();
                    clientCount++;

                    Console.WriteLine("Main: new connection...");

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