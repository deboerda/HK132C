using Google.Protobuf;
using GoogleProtobuf;
using UnityEngine;
using System.Net.Sockets;
using System;

public class TestClient : MonoBehaviour
{
    private TcpClient client;

    void Start()
    {
        StartCoroutine(ConnectAndSend());
    }

    System.Collections.IEnumerator ConnectAndSend()
    {
        yield return new WaitForSeconds(1);

        ConnectAndSendData();
        
        yield return new WaitForSeconds(3);
    }

    void ConnectAndSendData()
    {
        try
        {
            Debug.Log("Client: Connecting to server...");
            client = new TcpClient();
            client.Connect("127.0.0.1", 4545);
            Debug.Log("Client: Connected to server!");

            NetworkStream stream = client.GetStream();

            var animal = new Animal() { Age = 10 };
            byte[] protobufBytes = animal.ToByteArray();
            Debug.Log("Client: Protobuf data size: " + protobufBytes.Length);

            byte[] sizeBytes = BitConverter.GetBytes(protobufBytes.Length);
            Debug.Log("Client: Sending size: " + protobufBytes.Length);
            stream.Write(sizeBytes, 0, 4);

            Debug.Log("Client: Sending protobuf data...");
            stream.Write(protobufBytes, 0, protobufBytes.Length);
            stream.Flush();
            Debug.Log("Client: Data sent successfully!");

            stream.Close();
            client.Close();
            Debug.Log("Client: Disconnected");
        }
        catch (Exception ex)
        {
            Debug.LogError("Client error: " + ex.Message);
        }
    }

    void OnDestroy()
    {
        if (client != null && client.Connected)
        {
            client.Close();
        }
    }
}