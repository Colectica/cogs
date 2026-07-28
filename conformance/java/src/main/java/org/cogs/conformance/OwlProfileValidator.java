package org.cogs.conformance;

import java.io.File;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.semanticweb.owlapi.apibinding.OWLManager;
import org.semanticweb.owlapi.model.MissingImportHandlingStrategy;
import org.semanticweb.owlapi.model.OWLOntology;
import org.semanticweb.owlapi.model.OWLOntologyLoaderConfiguration;
import org.semanticweb.owlapi.model.OWLOntologyManager;
import org.semanticweb.owlapi.profiles.OWL2DLProfile;
import org.semanticweb.owlapi.profiles.OWLProfileReport;
import org.semanticweb.owlapi.profiles.OWLProfileViolation;

/** Loads generated Turtle through OWLAPI and requires the OWL 2 DL structural profile. */
public final class OwlProfileValidator {
    private static final String CARDINALITY_NAMES =
        "(?:cardinality|minCardinality|maxCardinality|qualifiedCardinality|" +
        "minQualifiedCardinality|maxQualifiedCardinality)";
    private static final Pattern CARDINALITY = Pattern.compile(
        "(?m)^[\\t ]*(?:owl:" + CARDINALITY_NAMES +
        "|<http://www\\.w3\\.org/2002/07/owl#" + CARDINALITY_NAMES + ">)" +
        "[\\t ]+\"([0-9]+)\"\\^\\^(?:xsd:nonNegativeInteger|" +
        "<http://www\\.w3\\.org/2001/XMLSchema#nonNegativeInteger>)");

    private OwlProfileValidator() { }

    public static void main(String[] args) {
        if (args.length == 1 && "--help".equals(args[0])) {
            System.out.println("Usage: java -jar owl-profile-validator.jar <ontology.ttl> [...]");
            return;
        }
        if (args.length == 1 && "--self-test".equals(args[0])) {
            runSelfTest();
            System.out.println("Turtle cardinality lexical self-test passed.");
            return;
        }
        if (args.length == 0) {
            System.err.println("Usage: java -jar owl-profile-validator.jar <ontology.ttl> [...]");
            System.exit(2);
        }

        boolean failed = false;
        for (String argument : args) {
            File file = new File(argument).getAbsoluteFile();
            try {
                if (!file.isFile()) {
                    throw new IllegalArgumentException("Ontology file not found");
                }
                OWLOntologyManager manager = OWLManager.createOWLOntologyManager();
                manager.setOntologyLoaderConfiguration(new OWLOntologyLoaderConfiguration()
                    .setMissingImportHandlingStrategy(MissingImportHandlingStrategy.THROW_EXCEPTION));
                OWLOntology ontology = manager.loadOntologyFromOntologyDocument(file);
                if (!ontology.getImportsDeclarations().isEmpty()) {
                    throw new IllegalStateException("Generated ontologies must be self-contained and must not import external ontologies");
                }
                if (ontology.getAxiomCount() == 0 || ontology.classesInSignature().findAny().isEmpty()) {
                    throw new IllegalStateException("Ontology has no class axioms");
                }

                BigInteger largestRawCardinality = largestRawCardinality(file);

                OWLProfileReport report = new OWL2DLProfile().checkOntology(ontology);
                if (!report.isInProfile()) {
                    List<OWLProfileViolation> violations = new ArrayList<>(report.getViolations());
                    violations.sort(Comparator.comparing(Object::toString));
                    System.err.println(file + ": OWL 2 DL profile violations:");
                    for (OWLProfileViolation violation : violations) {
                        System.err.println("  - " + violation);
                    }
                    failed = true;
                    continue;
                }

                System.out.printf(
                    "%s: OWLAPI parse and OWL 2 DL profile passed (%d axioms, %d classes).%n",
                    file,
                    ontology.getAxiomCount(),
                    ontology.classesInSignature().count());
                if (largestRawCardinality.compareTo(BigInteger.valueOf(Integer.MAX_VALUE)) > 0) {
                    System.out.printf(
                        "%s: raw Turtle cardinality %s was verified lexically; OWLAPI 5.5.1's int-backed " +
                        "object model cannot round-trip that standards-valid value losslessly.%n",
                        file,
                        largestRawCardinality);
                }
            } catch (Exception exception) {
                failed = true;
                System.err.println(file + ": " + exception.getClass().getSimpleName() + ": " + exception.getMessage());
            }
        }
        if (failed) {
            System.exit(1);
        }
    }

    private static BigInteger largestRawCardinality(File file) throws Exception {
        String turtle = Files.readString(file.toPath(), StandardCharsets.UTF_8);
        return largestRawCardinality(turtle);
    }

    private static BigInteger largestRawCardinality(String turtle) {
        Matcher matcher = CARDINALITY.matcher(turtle);
        BigInteger largest = BigInteger.ZERO;
        while (matcher.find()) {
            String lexical = matcher.group(1);
            if (lexical.length() > 1 && lexical.charAt(0) == '0') {
                throw new IllegalArgumentException("Non-canonical OWL cardinality lexical value: " + lexical);
            }
            BigInteger value = new BigInteger(lexical);
            if (value.compareTo(largest) > 0) {
                largest = value;
            }
        }
        return largest;
    }

    private static void runSelfTest() {
        String large = "999999999999999999999999999";
        String turtle =
            "@prefix owl: <http://www.w3.org/2002/07/owl#>.\n" +
            "@prefix xsd: <http://www.w3.org/2001/XMLSchema#>.\n" +
            "# owl:maxQualifiedCardinality \"777\"^^xsd:nonNegativeInteger\n" +
            "<urn:test> <urn:note> \"owl:maxQualifiedCardinality \\\"888\\\"^^xsd:nonNegativeInteger\";\n" +
            "  owl:minQualifiedCardinality \"2\"^^xsd:nonNegativeInteger;\n" +
            "  <http://www.w3.org/2002/07/owl#maxQualifiedCardinality> \"" + large +
            "\"^^<http://www.w3.org/2001/XMLSchema#nonNegativeInteger>.\n";
        if (!new BigInteger(large).equals(largestRawCardinality(turtle))) {
            throw new IllegalStateException("Turtle cardinality scanner did not preserve the largest value");
        }

        try {
            largestRawCardinality(
                "owl:qualifiedCardinality \"01\"^^xsd:nonNegativeInteger.\n");
            throw new IllegalStateException("Turtle cardinality scanner accepted a leading zero");
        } catch (IllegalArgumentException expected) {
            // Expected canonical-lexical rejection.
        }
    }
}
