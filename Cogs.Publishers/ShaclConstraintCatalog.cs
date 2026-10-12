using System;
using System.Collections.Generic;
using VDS.RDF;

namespace Cogs.Publishers;

/// <summary>Self-contained, publication-local standard SHACL-SPARQL components.</summary>
internal sealed class ShaclConstraintCatalog(Graph graph, string shapeBase)
{
    private const string Sh = "http://www.w3.org/ns/shacl#";
    // A fragment namespace keeps parameter local names portable. pySHACL 0.40.1
    // treats everything after the first '#' as the parameter name, including
    // slashes in model-owned shape namespaces. Definitions remain model-owned;
    // these predicates are only the reusable constraint vocabulary, not data.
    internal const string ParameterNamespace = "https://cogsdata.org/ns/shacl#";
    private readonly Dictionary<string, string> definitions = new(StringComparer.Ordinal);

    internal readonly record struct Parameter(string Name, INode? Value, string Datatype = "boolean", bool Optional = false);

    internal void Apply(INode shape, string name, string message, string body, params Parameter[] parameters)
    {
        string query = $"PREFIX rdf: <{NamespaceMapper.RDF}>\nPREFIX xsd: <{NamespaceMapper.XMLSCHEMA}>\nSELECT $this WHERE {{ {body} }}";
        string signature = query + "\n" + message + "\n" + string.Join(";", Array.ConvertAll(parameters, p => $"{p.Name}:{p.Datatype}:{p.Optional}"));
        if (definitions.TryGetValue(name, out string? existing))
        {
            if (existing != signature) throw new InvalidOperationException("Conflicting SHACL component definition: " + name);
        }
        else
        {
            definitions.Add(name, signature);
            INode component = Node(name + "ConstraintComponent");
            graph.Assert(component, Node(NamespaceMapper.RDF + "type", absolute: true), Node(Sh + "ConstraintComponent", absolute: true));
            Add(component, "name", graph.CreateLiteralNode(name));
            Add(component, "description", graph.CreateLiteralNode(message));
            Add(component, "labelTemplate", graph.CreateLiteralNode(message));
            foreach (Parameter parameter in parameters)
            {
                INode declaration = Node(name + "." + parameter.Name);
                Add(component, "parameter", declaration);
                graph.Assert(declaration, Node(NamespaceMapper.RDF + "type", absolute: true), Node(Sh + "Parameter", absolute: true));
                Add(declaration, "path", Node(ParameterNamespace + parameter.Name, absolute: true));
                Add(declaration, "name", graph.CreateLiteralNode(parameter.Name));
                if (parameter.Datatype == "IRI") Add(declaration, "nodeKind", Node(Sh + "IRI", absolute: true));
                else Add(declaration, "datatype", Node(NamespaceMapper.XMLSCHEMA + parameter.Datatype, absolute: true));
                if (parameter.Optional) Add(declaration, "optional", graph.CreateLiteralNode("true", UriFactory.Create(NamespaceMapper.XMLSCHEMA + "boolean")));
            }
            INode validator = Node(name + "Validator");
            Add(component, "nodeValidator", validator);
            graph.Assert(validator, Node(NamespaceMapper.RDF + "type", absolute: true), Node(Sh + "SPARQLSelectValidator", absolute: true));
            Add(validator, "message", graph.CreateLiteralNode(message));
            Add(validator, "select", graph.CreateLiteralNode(query));
        }
        foreach (Parameter parameter in parameters)
            if (parameter.Value is not null) graph.Assert(shape, Node(ParameterNamespace + parameter.Name, absolute: true), parameter.Value);
    }

    private IUriNode Node(string name, bool absolute = false) => graph.CreateUriNode(UriFactory.Create(absolute ? name : shapeBase + "constraint/" + name));
    private void Add(INode subject, string predicate, INode value) => graph.Assert(subject, Node(Sh + predicate, absolute: true), value);
}
