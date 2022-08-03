﻿using System;
using System.IO;
using System.Runtime.Serialization;

namespace NeeLaboratory.Runtime.Serialization
{
    public static class DefaultSerializer
    {
        public static byte[] Serialize<T>(T data)
        {
            using (var ms = new MemoryStream())
            {
                Serialize(ms, data);
                return ms.ToArray();
            }
        }

        public static void Serialize<T>(Stream stream, T data)
        {
            var serializer = new DataContractSerializer(typeof(T));
            serializer.WriteObject(stream, data);
        }

        public static T Deserialize<T>(byte[] source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            using (var ms = new MemoryStream(source))
            {
                return Deserialize<T>(ms);
            }
        }

        public static T Deserialize<T>(Stream stream)
        {
            var serializer = new DataContractSerializer(typeof(T));
            
            var instance = (T?)serializer.ReadObject(stream);
            if (instance is null) throw new FormatException();

            return instance;
        }
    }
}
