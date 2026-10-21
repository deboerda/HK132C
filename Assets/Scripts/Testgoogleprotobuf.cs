using Google.Protobuf;
using GoogleProtobuf;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

public class Testgoogleprotobuf : MonoBehaviour
{
    private TcpListener tcpListener;
    private bool isRunning;
    private Thread listenerThread;

    void Start()
    {
        StartServer();
    }

    void StartServer()
    {
        isRunning = true;
        Debug.Log("Creating TcpListener on 127.0.0.1:4545...");
        tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 4545);
        tcpListener.Start();
        Debug.Log("TcpListener started successfully");
        
        listenerThread = new Thread(new ThreadStart(ListenForClients));
        listenerThread.Start();
        Debug.Log("Listener thread started");
        
        Debug.Log("TCP Server started on 127.0.0.1:4545");
    }

    void ListenForClients()
    {
        Debug.Log("ListenForClients thread started, waiting for connections...");
        while (isRunning)
        {
            try
            {
                Debug.Log("Waiting for AcceptTcpClient...");
                TcpClient client = tcpListener.AcceptTcpClient();
                Debug.Log("Client accepted!");
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                clientThread.Start(client);
                Debug.Log("New client connected");
            }
            catch (SocketException ex)
            {
                if (isRunning)
                {
                    Debug.LogError("Socket error: " + ex.Message);
                }
            }
        }
    }

    void HandleClient(object clientObj)
    {
        TcpClient client = (TcpClient)clientObj;
        NetworkStream stream = client.GetStream();
        
        Debug.Log("Client handling started, Stream available: " + stream.CanRead);
        Debug.Log("Client endpoint: " + client.Client.RemoteEndPoint);
        
        byte[] sizeBuffer = new byte[4];
        int totalBytesRead = 0;
        
        while (isRunning && client.Connected)
        {
            try
            {
                Debug.Log("Waiting for size data...");
                totalBytesRead = stream.Read(sizeBuffer, 0, 4);
                Debug.Log("Read " + totalBytesRead + " bytes for size");
                
                if (totalBytesRead == 0)
                {
                    Debug.Log("Client closed connection");
                    break;
                }
                
                if (totalBytesRead != 4)
                {
                    Debug.LogError("Expected 4 bytes for size, got " + totalBytesRead);
                    break;
                }
                
                int messageSize = BitConverter.ToInt32(sizeBuffer, 0);
                Debug.Log("Message size: " + messageSize);
                
                byte[] messageBuffer = new byte[messageSize];
                totalBytesRead = 0;
                
                Debug.Log("Waiting for message data...");
                while (totalBytesRead < messageSize)
                {
                    int bytesRead = stream.Read(messageBuffer, totalBytesRead, messageSize - totalBytesRead);
                    Debug.Log("Read " + bytesRead + " bytes, total: " + totalBytesRead);
                    
                    if (bytesRead <= 0)
                    {
                        Debug.LogError("Connection lost while reading message");
                        break;
                    }
                    totalBytesRead += bytesRead;
                }
                
                if (totalBytesRead == messageSize)
                {
                    Debug.Log("Successfully read full message, parsing protobuf...");
                    Animal receivedAnimal = Animal.Parser.ParseFrom(messageBuffer);
                    Debug.Log("Received protobuf data: Age = " + receivedAnimal.Age);
                    Console.WriteLine("Received protobuf data: Age = " + receivedAnimal.Age);
                }
                else
                {
                    Debug.LogError("Incomplete message: expected " + messageSize + " bytes, got " + totalBytesRead);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Client handling error: " + ex.Message);
                Debug.LogError("Stack trace: " + ex.StackTrace);
                break;
            }
        }
        
        stream.Close();
        client.Close();
        Debug.Log("Client disconnected");
    }

    void OnDestroy()
    {
        StopServer();
    }

    void StopServer()
    {
        isRunning = false;
        
        if (tcpListener != null)
        {
            tcpListener.Stop();
            tcpListener = null;
        }
        
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join();
        }
        
        Debug.Log("TCP Server stopped");
    }
}
