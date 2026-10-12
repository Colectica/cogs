package org.cogs.conformance;

import java.nio.file.Path;
import java.nio.file.Files;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import org.apache.jena.atlas.json.JSON;
import org.apache.jena.atlas.json.JsonArray;
import org.apache.jena.atlas.json.JsonObject;
import org.apache.jena.atlas.json.JsonValue;
import org.apache.jena.rdf.model.ModelFactory;
import org.apache.jena.riot.RDFDataMgr;
import org.apache.jena.riot.Lang;
import org.apache.jena.shacl.Shapes;
import org.apache.jena.shacl.ValidationReport;

/** Independent SHACL Core/SPARQL gate; validates data alone without inference. */
public final class ShaclValidator {
    public static void main(String[] args) {
        if (args.length == 4 && args[0].equals("--advisory")) {
            advisory(args[1], args[2], args[3]);
            return;
        }
        if (args.length != 2) throw new IllegalArgumentException("Usage: shacl-validator <shapes.ttl> <manifest.json>");
        org.apache.jena.sys.JenaSystem.init();
        Shapes shapes = Shapes.parse(org.apache.jena.riot.RDFParser.source(args[0]).checking(false).toGraph());
        Path manifest = Path.of(args[1]).toAbsolutePath();
        int count = 0;
        for (JsonValue entry : JSON.read(manifest.toString()).get("cases").getAsArray()) {
            var test = entry.getAsObject();
            boolean expected = test.get("conforms").getAsBoolean().value();
            String name = test.get("name").getAsString().value();
            Shapes caseShapes = test.get("shapes") == null ? shapes
                : Shapes.parse(org.apache.jena.riot.RDFParser.source(test.get("shapes").getAsString().value()).checking(false).toGraph());
            // Preserve RDF lexical terms, including invalid test values and valid
            // COGS durations beyond Jena's native component-int parser. The shapes,
            // not the RDF parser's host value converter, enforce the COGS profile.
            var data = org.apache.jena.riot.RDFParser.source(manifest.getParent().resolve(test.get("file").getAsString().value()).toString()).checking(false).toGraph();
            ValidationReport report = org.apache.jena.shacl.ShaclValidator.get().validate(caseShapes, data);
            if (report.conforms() != expected) {
                System.err.println(name + ": expected conforms=" + expected);
                RDFDataMgr.write(System.err, ModelFactory.createModelForGraph(report.getGraph()), Lang.TURTLE);
                throw new AssertionError("SHACL mismatch: " + name);
            }
            count++;
        }
        System.out.println("Apache Jena SHACL: " + count + " cases passed.");
    }

    /** Experimental engine outcomes never substitute for the stable release gate. */
    private static void advisory(String defaultShapes, String manifestFile, String outputFile) {
        org.apache.jena.sys.JenaSystem.init();
        Path manifest = Path.of(manifestFile).toAbsolutePath();
        JsonObject result = new JsonObject();
        result.put("engine", "Apache Jena");
        JsonArray outcomes = new JsonArray();
        result.put("cases", outcomes);
        var cache = new HashMap<String, Shapes>();
        var failures = new HashMap<String, String>();
        for (JsonValue entry : JSON.read(manifest.toString()).get("cases").getAsArray()) {
            var test = entry.getAsObject();
            var outcome = new JsonObject();
            outcome.put("name", test.get("name").getAsString().value());
            boolean expected = test.get("conforms").getAsBoolean().value();
            outcome.put("expected", expected);
            String source = test.get("shapes") == null ? defaultShapes : test.get("shapes").getAsString().value();
            try {
                if (failures.containsKey(source)) throw new IllegalArgumentException(failures.get(source));
                Shapes shapes = cache.get(source);
                if (shapes == null) {
                    try {
                        shapes = Shapes.parse(org.apache.jena.riot.RDFParser.source(source).checking(false).toGraph());
                        cache.put(source, shapes);
                    } catch (Exception error) {
                        failures.put(source, error.toString());
                        throw error;
                    }
                }
                var data = org.apache.jena.riot.RDFParser.source(manifest.getParent().resolve(test.get("file").getAsString().value()).toString()).checking(false).toGraph();
                ValidationReport report = org.apache.jena.shacl.ShaclValidator.get().validate(shapes, data);
                outcome.put("actual", report.conforms());
                outcome.put("status", report.conforms() == expected ? "matched" : "mismatched");
                ByteArrayOutputStream text = new ByteArrayOutputStream();
                RDFDataMgr.write(text, ModelFactory.createModelForGraph(report.getGraph()), Lang.TURTLE);
                outcome.put("report", text.toString(StandardCharsets.UTF_8));
            } catch (Exception error) {
                outcome.put("status", "error");
                outcome.put("error", error.toString());
            }
            outcomes.add(outcome);
            try { Files.writeString(Path.of(outputFile), result.toString(), StandardCharsets.UTF_8); }
            catch (Exception error) { throw new IllegalStateException("Cannot save advisory report", error); }
        }
        System.out.println("Apache Jena SHACL 1.2 advisory outcomes recorded: " + outcomes.size());
    }
}
