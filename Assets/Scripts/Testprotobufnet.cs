using System.IO;
using Protobufnet;
using UnityEngine;

public class Testprotobufnet : MonoBehaviour
{
    void Start()
    {
        var p = new Person { Age = 100 };
        
        // 序列化数据
        byte[] bytes;
        using (var bodyMemory = new MemoryStream())
        {
            ProtoBuf.Serializer.Serialize(bodyMemory, p);
            bytes = bodyMemory.ToArray();
        }

        // 反序列化数据
        var s2 = ProtoBuf.Serializer.Deserialize<Person>(new MemoryStream(bytes));
        Debug.Log(s2.Age);
    }

}
