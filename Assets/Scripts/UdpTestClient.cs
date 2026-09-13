using Google.Protobuf;
using GoogleProtobuf;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public class UdpTestClient : MonoBehaviour
{
    private UdpClient udpClient;

    void Start()
    {
        StartCoroutine(SendMessage());
    }

    System.Collections.IEnumerator SendMessage()
    {
        yield return new WaitForSeconds(1);

        try
        {
            udpClient = new UdpClient();
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4545);

            var animal = new Animal() { Age = 20 };
            byte[] protobufBytes = animal.ToByteArray();
            Debug.Log("UDP Client: Sending protobuf data, size: " + protobufBytes.Length);

            udpClient.Send(protobufBytes, protobufBytes.Length, serverEndPoint);
            Debug.Log("UDP Client: Data sent successfully to 127.0.0.1:4545");

            udpClient.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError("UDP Client error: " + ex.Message);
        }
    }
}