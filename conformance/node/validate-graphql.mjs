import { readFile } from "node:fs/promises";
import { buildSchema, validateSchema } from "graphql";

if (process.argv.length < 3) {
  throw new Error("Pass at least one generated GraphQL schema path.");
}

for (const path of process.argv.slice(2)) {
  const source = await readFile(path, "utf8");
  const schema = buildSchema(source);
  const errors = validateSchema(schema);
  if (errors.length !== 0) {
    throw new Error(`${path}:\n${errors.map((error) => error.message).join("\n")}`);
  }
  console.log(`PASS GraphQL ${path}`);
}
