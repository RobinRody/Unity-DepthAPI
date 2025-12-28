using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace GM{
    [System.Serializable]
    public class JsonFuncBase{
        public JsonFuncBase(){

        }

        public string ToJson(){
            return UnityNewtonsoftJsonSerializer.Serialize(this, true);
            // return JsonUtility.ToJson(this, true);
        }

        public void LoadFromJson(string getJason){
            JsonUtility.FromJsonOverwrite(getJason, this);
            //// 若要支援 Dictionary，請改用
            //UnityNewtonsoftJsonSerializer.PopulateObject(getJason, this);
            //// 或
            // //JsonConvert.PopulateObject(getJason, this); // 若直接用 Newtonsoft.Json
        }
    }

    [System.Serializable]
    public class NetworkByteBase : JsonFuncBase{
        public NetworkByteBase(){

        }

        public byte[] ToByteArray(){
            BinaryFormatter bf = new BinaryFormatter();

            using( var ms = new MemoryStream() )
            {
                bf.Serialize(ms, this);
                return ms.ToArray();
            }
        }

        public static object ToObject(object obj){
            byte[] arrBytes = (byte[]) obj;
            
            using( var memStream = new MemoryStream() )
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                return binForm.Deserialize(memStream);
            }
        }
    }
}
