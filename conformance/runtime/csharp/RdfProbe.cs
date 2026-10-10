using Cogs.Conformance.Model;
using Cogs.Common;
using Cogs.DataAnnotations;
using System.Collections;
using System.Reflection;
using System.Xml.Linq;
using VDS.RDF;

internal static class RdfProbe
{
    internal static void Run(ItemContainer container)
    {
        IGraph graph = container.MakeRdfGraph();
        XElement[] definitions = XDocument.Parse(container.ToXml()).Root!.Elements()
            .Where(element => element.Name.LocalName != "TopLevelReference").ToArray();
        for (int index = 0; index < container.Items.Count; index++)
            Check(container.Items[index], definitions[index], Node(RdfUriFactory.GetUri(container.Items[index])));
        Console.WriteLine("PASS C# RDF collections: order, multiplicity, membership, lexical values, nested/inherited fields");

        IUriNode Node(string iri) => graph.CreateUriNode(UriFactory.Create(iri));
        void Check(object value, XElement xml, INode subject)
        {
            string termBase = CogsRdfNaming.GetTermBase(xml.Name.NamespaceName);
            foreach (PropertyInfo property in value.GetType().GetProperties())
            {
                CogsPropertyAttribute? metadata = property.GetCustomAttribute<CogsPropertyAttribute>();
                if (metadata is null) continue;
                XElement[] elements = xml.Elements().Where(element => element.Name.LocalName == metadata.Name).ToArray();
                object? data = property.GetValue(value);
                object[] values = data is IList list ? list.Cast<object>().ToArray() : data is null ? [] : [data];
                string local = CogsRdfNaming.ToPropertyLocalName(metadata.Name);
                INode[] members = graph.GetTriplesWithSubjectPredicate(subject, Node(termBase + local))
                    .Select(triple => triple.Object).ToArray();
                if (metadata.Ordered)
                {
                    INode[] heads = graph.GetTriplesWithSubjectPredicate(subject, Node(termBase + local + "List"))
                        .Select(triple => triple.Object).ToArray();
                    Require(heads.Length == 1, "Expected one companion head: " + metadata.Name);
                    INode[] ordered = graph.GetListItems(heads[0]).ToArray();
                    Require(ordered.Length == values.Length && ordered.Length == elements.Length,
                        "Occurrence count changed: " + metadata.Name);
                    Require(members.ToHashSet().SetEquals(ordered), "Membership differs from list: " + metadata.Name);
                    for (int index = 0; index < ordered.Length; index++)
                    {
                        if (metadata.Kind == CogsPropertyKind.Primitive)
                        {
                            Require(ordered[index] is ILiteralNode, "Primitive list member is not a literal.");
                            var literal = (ILiteralNode)ordered[index];
                            Require(literal.Value == elements[index].Value, "Primitive list order/value changed: " + metadata.Name);
                        }
                        else if (metadata.Kind == CogsPropertyKind.ItemReference)
                            Require(ordered[index].Equals(Node(RdfUriFactory.GetUri((IIdentifiable)values[index]))),
                                "Item reference order/identity changed: " + metadata.Name);
                        else Check(values[index], elements[index], ordered[index]);
                    }
                }
                else if (metadata.Kind == CogsPropertyKind.Composite && values.Length == 1)
                {
                    Require(members.Length == 1, "Expected a singleton composite: " + metadata.Name);
                    Check(values[0], elements[0], members[0]);
                }
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
