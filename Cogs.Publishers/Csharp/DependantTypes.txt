using Cogs.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using VDS.RDF;

namespace __CogsGeneratedNamespace
{

    /// <summary>
    /// Store one instance of each identified item as it is being handled
    /// </summary>
    public class CogsItemCacheFactory
    {
        private Dictionary<string, object> cache = new Dictionary<string, object>();

        public T GetByReference<T>(RawJsonReference reference) where T : class, new()
        {
            var cacheKey = JsonReferenceHelper.GetCacheKey(reference);
            if (cache.TryGetValue(cacheKey, out object? existingItem))
            {
                if (existingItem is T typedItem)
                {
                    return typedItem;
                }
                else
                {
                    throw new InvalidOperationException("Id lookup found item of wrong type");
                }
            }
            T item = new T();
            cache.Add(cacheKey, item);
            return item as T;
        }

        public object? GetByReference(RawJsonReference reference, Type t)
        {
            var cacheKey = JsonReferenceHelper.GetCacheKey(reference);
            if (cache.TryGetValue(cacheKey, out object? existingItem))
            {
                if (existingItem.GetType() == t)
                {
                    return existingItem;
                }
                else
                {
                    throw new InvalidOperationException("Id lookup found item of wrong type");
                }
            }
            object? item = Activator.CreateInstance(t);

            if (item != null)
            {
                cache.Add(cacheKey, item);
            }

            return item;
        }
    }

    public static class JsonReferenceHelper
    {
        public static JObject CreateReferenceObject(IIdentifiable item, JsonSerializer serializer)
        {
            var reference = new JObject
            {
                ["$type"] = item.GetType().Name
            };

            foreach (var propertyName in ItemContainer.JsonReferencePropertyNames)
            {
                var propertyInfo = item.GetType().GetProperty(propertyName);
                var value = propertyInfo?.GetValue(item);
                reference[propertyName] = value == null ? JValue.CreateNull() : JToken.FromObject(value, serializer);
            }

            return reference;
        }

        public static RawJsonReference ToReference(JObject jsonObject)
        {
            var reference = new RawJsonReference
            {
                ItemType = (string?)jsonObject["$type"] ?? string.Empty
            };

            foreach (var propertyName in ItemContainer.JsonReferencePropertyNames)
            {
                if (jsonObject.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out JToken? value))
                {
                    reference.ReferenceValues[propertyName] = value;
                }
            }

            return reference;
        }

        public static string GetCacheKey(RawJsonReference reference)
        {
            var parts = new List<string> { reference.ItemType };
            foreach (var propertyName in ItemContainer.JsonReferencePropertyNames)
            {
                if (reference.ReferenceValues.TryGetValue(propertyName, out JToken? value))
                {
                    parts.Add(value?.Type == JTokenType.Null ? string.Empty : value?.ToString() ?? string.Empty);
                }
                else
                {
                    parts.Add(string.Empty);
                }
            }

            return string.Join("|", parts);
        }

        public static Type? ResolveItemType(string itemTypeName)
        {
            if (string.IsNullOrWhiteSpace(itemTypeName))
            {
                return null;
            }

            return Type.GetType($"{ItemContainer.ModelNamespace}.{itemTypeName}");
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

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;

            if (value is ItemContainer container)
            {
                writer.WriteStartObject();

                if (container.TopLevelReferences.Count > 0)
                {
                    var topLevel = new JProperty("topLevelReferences",
                        new JArray(
                            from obj in container.TopLevelReferences
                            select JsonReferenceHelper.CreateReferenceObject(obj, serializer)));
                    topLevel.WriteTo(writer);
                }

                var items = new JProperty("items",
                    new JArray(
                        from obj in container.Items
                        select CreateSerializedItemObject(obj, serializer)));
                items.WriteTo(writer);

                writer.WriteEndObject();
            }
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            ItemContainer container = new ItemContainer();
            ItemContainer.AsyncLocalItemCache.Value = new CogsItemCacheFactory();

            try
            {
                reader.DateParseHandling = DateParseHandling.None;
                JObject containerObject = JObject.Load(reader);

                if (containerObject["items"] is JArray items)
                {
                    foreach (var itemToken in items.Children<JObject>())
                    {
                        var reference = JsonReferenceHelper.ToReference(itemToken);
                        Type? requestedType = JsonReferenceHelper.ResolveItemType(reference.ItemType);
                        if (requestedType == null)
                        {
                            continue;
                        }

                        object? item = ItemContainer.AsyncLocalItemCache.Value?.GetByReference(reference, requestedType);
                        if (itemToken != null && item != null)
                        {
                            JsonConvert.PopulateObject(itemToken.ToString(), item);
                            if (item is IIdentifiable identifiableItem)
                            {
                                container.Items.Add(identifiableItem);
                            }
                        }
                    }
                }

                if (containerObject["topLevelReferences"] is JArray topLevelReferences)
                {
                    foreach (var topLevelToken in topLevelReferences.Children())
                    {
                        RawJsonReference? reference = topLevelToken.ToObject<RawJsonReference>();
                        if (reference == null)
                        {
                            continue;
                        }

                        Type? requestedType = JsonReferenceHelper.ResolveItemType(reference.ItemType);
                        if (requestedType == null)
                        {
                            continue;
                        }

                        var item = ItemContainer.AsyncLocalItemCache.Value?.GetByReference(reference, requestedType);
                        if (item is IIdentifiable identifiableItem)
                        {
                            container.TopLevelReferences.Add(identifiableItem);
                        }
                    }
                }

                return container;
            }
            finally
            {
                ItemContainer.AsyncLocalItemCache.Value = null;
            }
        }

        private static JObject CreateSerializedItemObject(IIdentifiable item, JsonSerializer serializer)
        {
            var itemObject = new JObject
            {
                ["$type"] = item.GetType().Name
            };

            foreach (var property in JObject.FromObject(item, serializer).Properties())
            {
                if (string.Equals(property.Name, "$type", StringComparison.Ordinal))
                {
                    continue;
                }

                itemObject.Add(property.Name, property.Value);
            }

            return itemObject;
        }
    }

    public class IIdentifiableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType is IIdentifiable;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is IList list)
            {
                writer.WriteStartArray();

                foreach (var i in list)
                {                   
                    if (i is IIdentifiable item)
                    {
                        JsonReferenceHelper.CreateReferenceObject(item, serializer).WriteTo(writer);
                    }
                }
                writer.WriteEndArray();
            }
            else if (value is IIdentifiable item)
            {
                JsonReferenceHelper.CreateReferenceObject(item, serializer).WriteTo(writer);
            }
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // Read a list of references
                List<RawJsonReference>? multiReference = serializer.Deserialize<List<RawJsonReference>>(reader);

                object? result = Activator.CreateInstance(objectType);
                if (result is IList list && multiReference != null)
                {
                    foreach (var r in multiReference)
                    {
                        Type? requestedType = JsonReferenceHelper.ResolveItemType(r.ItemType);
                        if (requestedType == null)
                        {
                            return null;
                        }

                        object? item = ItemContainer.AsyncLocalItemCache.Value?.GetByReference(r, requestedType);
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
                RawJsonReference? r = serializer.Deserialize<RawJsonReference>(reader);
                if (r == null)
                {
                    return null;
                }

                Type? requestedType = JsonReferenceHelper.ResolveItemType(r.ItemType);
                if (requestedType == null)
                {
                    return null;
                }

                object? item = ItemContainer.AsyncLocalItemCache.Value?.GetByReference(r, requestedType);
                return item;
            }
        }
    }

    public class SubstitutionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true; // maybe make a ITypeDiscriminator
        }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }


        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var results = Activator.CreateInstance(objectType);
                if (results is IList list)
                {
                    var array = JArray.Load(reader);
                    foreach (var item in array.Children())
                    {
                        var jsonObject = (JObject)item;
                        list.Add(FromTypeDiscriminatedObject(jsonObject));
                    }
                }
                else
                {
                    throw new InvalidOperationException("All generated collections should implement IList");
                }
                return results;
            }

            var single = JObject.Load(reader);
            return FromTypeDiscriminatedObject(single);
        }

        private object? FromTypeDiscriminatedObject(JObject jsonObject)
        {
            string? typeDiscriminator = (string?)jsonObject["$type"];
            Type? requestedType = JsonReferenceHelper.ResolveItemType(typeDiscriminator ?? string.Empty);
            if (requestedType == null)
            {
                return null;
            }

            var result = jsonObject.ToObject(requestedType);
            return result;
        }
    }

    /// <summary>
    /// Read json style item references
    /// </summary>
    public class RawJsonReference
    {
        [JsonProperty("$type")]
        public string ItemType { get; set; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JToken?> ReferenceValues { get; set; } = new Dictionary<string, JToken?>(StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// IIdentifiable class which all object Inherit from. Used to Serialize to Json
    /// <summary>
    public partial interface IIdentifiable
    {
        string ReferenceId { get; }
        XElement ToXml();
        INode AddTriples(IGraph graph, INode? itemNode = null);
        string GetUri() { return RdfUriFactory.GetUri(this); }
    }


    public static class RdfUriFactory
    {
        public static string Prefix { get; set; } = "https://example.org";

        public static string GetUri(IIdentifiable identifiable)
        {
            return Prefix + identifiable.ReferenceId;
        }
    }

    /// <summary>
    /// Class that contains a list of all items in the model 
    /// <summary>
    [JsonConverter(typeof(ItemContainerConverter))]
    public partial class ItemContainer
    {
        public List<IIdentifiable> Items { get; } = new List<IIdentifiable>();
        public List<IIdentifiable> TopLevelReferences { get; } = new List<IIdentifiable>();
        internal static string[] JsonReferencePropertyNames { get; } = new string[] { "__JsonReferencePropertyNames__" };

        internal static AsyncLocal<CogsItemCacheFactory?> AsyncLocalItemCache { get; } = new AsyncLocal<CogsItemCacheFactory?>();

        internal static string ModelNamespace { get; set; } = "__CogsGeneratedNamespace";        
    }
}
