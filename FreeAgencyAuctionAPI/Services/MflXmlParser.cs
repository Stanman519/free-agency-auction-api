using System;
using System.IO;
using System.Xml.Serialization;

namespace FreeAgencyAuctionAPI.Services
{
    public static class MflXmlParser
    {
        public static T XmlDeserializeFromString<T>(this string objectData)
        {
            return (T)XmlDeserializeFromString(objectData, typeof(T));
        }
        public static object XmlDeserializeFromString(this string objectData, Type type)
        {
            var serializer = new XmlSerializer(type);
            object result;

            using (TextReader reader = new StringReader(objectData))
            {
                result = serializer.Deserialize(reader);
            }

            return result;
        }
    }

    [XmlRoot(ElementName="error")]
    public class MflXmlError
    {
        [XmlText]
        public string ErrorMsg { get; set; }
    }
}