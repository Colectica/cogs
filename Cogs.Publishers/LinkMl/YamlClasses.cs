using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Cogs.Publishers.LinkMl;

/// <summary>
/// Writes arbitrary-precision cardinalities as YAML integer scalars instead of
/// reflecting the implementation properties of <see cref="BigInteger"/>.
/// </summary>
public sealed class BigIntegerYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(BigInteger);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        Scalar scalar = parser.Consume<Scalar>();
        return BigInteger.Parse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is not BigInteger integer)
        {
            throw new InvalidOperationException("A LinkML cardinality must be an integer.");
        }

        emitter.Emit(new Scalar(integer.ToString(CultureInfo.InvariantCulture)));
    }
}

public sealed class LinkMLModel
{
    public required string id { get; init; }
    public required string name { get; init; }
    public Dictionary<string, string> prefixes { get; init; } = new();
    public string[] imports { get; init; } = ["linkml:types"];
    public string default_range { get; init; } = "string";
    public required string default_prefix { get; init; }
    public Dictionary<string, LinkMLClass> classes { get; init; } = new();
    public Dictionary<string, LinkMLSlot> slots { get; init; } = new();
    public Dictionary<string, LinkMLType> types { get; init; } = new();
}

public sealed class LinkMLClass
{
    public string? description { get; init; }
    public string? is_a { get; init; }

    [YamlMember(Alias = "abstract")]
    public bool IsAbstract { get; init; }

    public string? deprecated { get; init; }
    public Dictionary<string, LinkMLSlot> slot_usage { get; init; } = new();
    public List<string> slots { get; init; } = new();
    public Dictionary<string, LinkMLUniqueKeySlots> unique_keys { get; init; } = new();
}

public sealed class LinkMLSlot
{
    public string? slot_uri { get; init; }
    public string? description { get; init; }
    public string? range { get; init; }
    public string? deprecated { get; init; }
    public string? pattern { get; init; }
    public object? minimum_value { get; init; }
    public object? maximum_value { get; init; }
    public BigInteger? minimum_cardinality { get; init; }
    public BigInteger? maximum_cardinality { get; init; }
    public bool required { get; init; }
    public bool? multivalued { get; init; }
    public bool? inlined { get; init; }
    public bool? inlined_as_list { get; init; }
    public bool? list_elements_ordered { get; init; }
    public List<string>? equals_string_in { get; init; }
}

public sealed class LinkMLType
{
    public string? description { get; init; }
    [YamlMember(Alias = "typeof")]
    public string? TypeOf { get; init; }
    public string? uri { get; init; }
    public string? pattern { get; init; }
    public List<string>? union_of { get; init; }
    public object? minimum_value { get; init; }
    public object? maximum_value { get; init; }
}

public sealed class LinkMLUniqueKeySlots
{
    public List<string> unique_key_slots { get; init; } = new();
}
