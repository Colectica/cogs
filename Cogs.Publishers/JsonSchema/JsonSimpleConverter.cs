using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cogs.Publishers.JsonSchema
{
    public class JsonSimpleConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is string schema)
            {
                var GMD = new JArray() { "month", "day" };
                var GYM = new JArray() { "year", "month" };
                var obj = new JObject();
                obj.Add(new JProperty("duration", 
                    new JObject(
                        new JProperty("type", "number"), 
                        new JProperty("format", "utc-millisec"))));

                obj.Add(new JProperty("dateTime", 
                    new JObject(
                        new JProperty("type", "string"), 
                        new JProperty("format", "date-time"))));

                obj.Add(new JProperty("time", 
                    new JObject(
                        new JProperty("type", "string"), 
                    new JProperty("format", "time"))));

                obj.Add(new JProperty("date", 
                    new JObject(
                        new JProperty("type", "string"), 
                        new JProperty("format", "date"))));

                obj.Add(new JProperty("nonNegativeInteger",
                    new JObject(
                        new JProperty("type", "integer"),
                        new JProperty("minimum", 0))));

                obj.Add(new JProperty("gYearMonth", 
                    new JObject(
                        new JProperty("type", "object"), 
                        new JProperty("properties", 
                        new JObject(
                            new JProperty("year", 
                            new JObject(
                                new JProperty("type", "integer"))), 
                            new JProperty("month", 
                            new JObject(
                                new JProperty("type", "integer"))), 
                            new JProperty("timezone", 
                            new JObject(
                                new JProperty("type", "string"), new JProperty("pattern", @"^(Z)|((\+|\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$"))))), 
                        new JProperty("required", GYM), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("gYear", 
                    new JObject(
                        new JProperty("type", "object"), 
                        new JProperty("properties", 
                        new JObject(
                            new JProperty("year", 
                            new JObject(
                                new JProperty("type", "integer"))), 
                            new JProperty("timezone", 
                            new JObject(
                                new JProperty("type", "string"), new JProperty("pattern", @"^(Z)|((\+|\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$"))))), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("gMonthDay", 
                    new JObject(
                        new JProperty("type", "object"), 
                        new JProperty("properties", 
                            new JObject(
                                new JProperty("month",
                                    new JObject(
                                        new JProperty("type", "integer"))), 
                                new JProperty("day", 
                                    new JObject(
                                        new JProperty("type", "integer"))), 
                                new JProperty("timezone", 
                                    new JObject(
                                        new JProperty("type", "string"), new JProperty("pattern", @"^(Z)|((\+|\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$"))))), 
                                new JProperty("required", GMD), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("gDay", 
                    new JObject(
                        new JProperty("type", "object"),
                    new JProperty("properties", 
                    new JObject(
                        new JProperty("day", 
                        new JObject(
                            new JProperty("type", "integer"))), 
                        new JProperty("timezone", 
                        new JObject(
                            new JProperty("type", "string"), new JProperty("pattern", @"^(Z)|((\+|\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$"))))), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("gMonth", 
                    new JObject(
                        new JProperty("type", "object"), 
                    new JProperty("properties", 
                    new JObject(
                        new JProperty("month", 
                        new JObject(
                            new JProperty("type", "integer"))), 
                        new JProperty("timezone", 
                        new JObject(
                            new JProperty("type", "string"), new JProperty("pattern", @"^(Z)|((\+|\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$"))))), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("anyURI", 
                    new JObject(
                        new JProperty("type", "string"))));

                obj.Add(new JProperty("cogsDate",
                    new JObject(
                        new JProperty("type", "object"),
                        new JProperty("properties", 
                        new JObject(
                            new JProperty("dateTime", 
                            new JObject(
                                new JProperty("$ref", "#/simpleType/dateTime"))),
                            new JProperty("date", 
                            new JObject(
                                new JProperty("$ref", "#/simpleType/date"))),
                            new JProperty("gYearMonth", 
                            new JObject(
                                new JProperty("$ref", "#/simpleType/gYearMonth"))),
                            new JProperty("gYear", 
                            new JObject(
                                new JProperty("$ref", "#/simpleType/gYear"))),
                            new JProperty("duration", 
                            new JObject(
                                new JProperty("$ref", "#/simpleType/duration"))))), new JProperty("additionalProperties", false))));

                obj.Add(new JProperty("language", 
                    new JObject(
                        new JProperty("type","string"))));

                obj.Add(new JProperty("langString",
                    new JObject(
                        new JProperty("type", "object"),
                        new JProperty("properties",
                        new JObject(
                            new JProperty("languageTag",
                                new JObject(
                                new JProperty("type", "string"))),
                            new JProperty("value",
                                new JObject(
                                new JProperty("type", "string"))))), new JProperty("additionalProperties", false))));


                obj.WriteTo(writer);
            }
        }
    }
}