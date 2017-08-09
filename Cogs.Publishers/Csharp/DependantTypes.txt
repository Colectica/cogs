using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace __CogsGeneratedNamespace
{

    /// <summary>
    /// Store one instance of each identified item as it is being handled
    /// </summary>
    public class CogsItemCacheFactory
    {
        private Dictionary<string, object> cache = new Dictionary<string, object>();

        public T GetByReferenceId<T>(string referenceId) where T : class, new()
        {
            object item = null;
            if (cache.TryGetValue(referenceId, out item))
            {
                if (item is T typedItem)
                {
                    return typedItem;
                }
                else
                {
                    throw new InvalidOperationException("Id lookup found item of wrong type");
                }
            }
            item = new T();
            cache.Add(referenceId, item);
            return item as T;
        }

        public object GetByReferenceId(string referenceId, Type t)
        {
            object item = null;
            if (cache.TryGetValue(referenceId, out item))
            {
                if (item.GetType() == t)
                {
                    return item;
                }
                else
                {
                    throw new InvalidOperationException("Id lookup found item of wrong type");
                }
            }
            item = Activator.CreateInstance(t);
            cache.Add(referenceId, item);
            return item;
        }
    }

    public class ItemContainerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ItemContainer);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;

            if (value is ItemContainer container)
            {
                writer.WriteStartObject();

                if (container.TopLevelReferences.Count > 0)
                {
                    var topLevel = new JProperty("TopLevelReference",
                        new JArray(
                            from obj in container.TopLevelReferences
                            select new JObject(
                                new JProperty("$type", "ref"),
                                new JProperty("value", new JArray(
                                    obj.GetType().Name.ToString(),
                                    obj.ReferenceId)))));
                    topLevel.WriteTo(writer);
                }

                var groups = container.Items.GroupBy(x => x.GetType().Name);
                if (groups.Count() > 0)
                {
                    foreach (var group in groups)
                    {
                        var classGrouping = new JObject();
                        foreach (var element in group)
                        {
                            JObject itemObject = JObject.FromObject(element, serializer);
                            JProperty json = new JProperty(element.ReferenceId, itemObject);
                            classGrouping.Add(json);
                        }

                        writer.WritePropertyName(group.Key);
                        classGrouping.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            ItemContainer container = new ItemContainer();
            ItemContainer.AsyncLocalItemCache.Value = new CogsItemCacheFactory();

            reader.DateParseHandling = DateParseHandling.None;
            JObject containerObject = JObject.Load(reader);

            foreach (var type in containerObject)
            {
                if (type.Key.Equals("TopLevelReference"))
                {
                    var topLevelJsonReferences = type.Value.ToObject<List<RawJsonReference>>();
                    foreach (var r in topLevelJsonReferences)
                    {
                        Type requestedType = Type.GetType($"{ ItemContainer.ModelNamespace}.{r.ItemType}");
                        var item = ItemContainer.AsyncLocalItemCache.Value.GetByReferenceId(r.IdString, requestedType);
                        if (item is IIdentifiable identifiableItem)
                        {
                            container.TopLevelReferences.Add(identifiableItem);
                        }
                    }
                }
                else
                {
                    var containerType = type.Key;
                    foreach (KeyValuePair<string, JToken> instance in (JObject)type.Value)
                    {
                        Type requestedType = Type.GetType($"{ItemContainer.ModelNamespace}.{containerType}");
                        var item = ItemContainer.AsyncLocalItemCache.Value.GetByReferenceId(instance.Key, requestedType);

                        JsonConvert.PopulateObject(instance.Value.ToString(), item);
                        if (item is IIdentifiable identifiableItem)
                        {
                            container.Items.Add(identifiableItem);
                        }
                    }
                }
            }

            ItemContainer.AsyncLocalItemCache.Value = null;
            return container;
        }
    }

    public class IIdentifiableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType is IIdentifiable;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is IList list)
            {
                writer.WriteStartArray();

                foreach (var i in list)
                {                   
                    if (i is IIdentifiable item)
                    {
                        var reference = new JObject(
                            new JProperty("$type", "ref"),
                            new JProperty("value",
                                new JArray(
                                    item.GetType().Name,
                                    item.ReferenceId)));

                        reference.WriteTo(writer);
                    }
                }
                writer.WriteEndArray();
            }
            else if (value is IIdentifiable item)
            {
                var reference = new JObject(
                            new JProperty("$type", "ref"),
                            new JProperty("value",
                                new JArray(
                                    item.GetType().Name,
                                    item.ReferenceId)));

                reference.WriteTo(writer);
            }
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // Read a list of references
                var multiReference = serializer.Deserialize<List<RawJsonReference>>(reader);

                var result = Activator.CreateInstance(objectType);
                if (result is IList list)
                {
                    foreach (var r in multiReference)
                    {
                        Type requestedType = Type.GetType($"{ ItemContainer.ModelNamespace}.{ r.ItemType}");
                        var item = ItemContainer.AsyncLocalItemCache.Value.GetByReferenceId(r.IdString, requestedType);
                        list.Add(item);
                    }
                }
                else
                {
                    throw new InvalidOperationException("All generated collections should implement IList");
                }
                return result;
            }
            else
            {
                // Read a single reference
                var r = serializer.Deserialize<RawJsonReference>(reader);

                Type requestedType = Type.GetType($"{ ItemContainer.ModelNamespace}.{ r.ItemType}");
                var item = ItemContainer.AsyncLocalItemCache.Value.GetByReferenceId(r.IdString, requestedType);

                return item;
            }
        }
    }
    
    /// <summary>
    /// Read json style item references
    /// </summary>
    public class RawJsonReference
    {
        [JsonProperty("$type")]
        public string SpecialRefValue { get; set; } = "ref";

        [JsonProperty("value")]
        public List<string> IdParts { get; set; }

        [JsonIgnore]
        public string ItemType
        {
            get { return IdParts[0]; }
        }
        [JsonIgnore]
        public string IdString
        {
            get { return IdParts[1]; }
        }
    }
    
    /// <summary>
    /// IIdentifiable class which all object Inherit from. Used to Serialize to Json
    /// <summary>
    public partial interface IIdentifiable
    {
        string ReferenceId { get; }
        XElement ToXml();
    }

    /// <summary>
    /// Class that contains a list of all items in the model 
    /// <summary>
    [JsonConverter(typeof(ItemContainerConverter))]
    public partial class ItemContainer
    {
        public List<IIdentifiable> Items { get; } = new List<IIdentifiable>();
        public List<IIdentifiable> TopLevelReferences { get; } = new List<IIdentifiable>();

        internal static AsyncLocal<CogsItemCacheFactory> AsyncLocalItemCache { get; } = new AsyncLocal<CogsItemCacheFactory>();

        internal static string ModelNamespace { get; set; } = "__CogsGeneratedNamespace";        
    }
}
