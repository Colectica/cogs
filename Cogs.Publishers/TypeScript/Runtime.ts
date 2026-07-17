import {
  DOMImplementation,
  DOMParser,
  XMLSerializer,
  onWarningStopParsing,
  type Document,
  type Element,
  type Node,
} from "@xmldom/xmldom";
import { readFile, writeFile, type PathLike } from "node:fs";
import type { Readable, Writable } from "node:stream";

const TARGET_NAMESPACE = __TARGET_NAMESPACE__;
const NAMESPACE_PREFIX = __NAMESPACE_PREFIX__;
const XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance";
const XML_NAMESPACE = "http://www.w3.org/XML/1998/namespace";
const XMLNS_NAMESPACE = "http://www.w3.org/2000/xmlns/";

interface IdentificationField {
  readonly cogsName: string;
  readonly attributeName: string;
}

const IDENTIFICATION_FIELDS: readonly IdentificationField[] = __IDENTIFICATION_FIELDS__;

export interface FieldSpec {
  readonly cogsName: string;
  readonly attributeName: string;
  readonly description: string;
  readonly typeName: string;
  readonly kind: "simple" | "item" | "object";
  readonly many: boolean;
  readonly ordered: boolean;
  readonly allowSubtypes: boolean;
}

interface CogsConstructor<T extends CogsValue = CogsValue> {
  readonly prototype: T;
  readonly cogsType: string;
  readonly isItem: boolean;
  readonly isAbstract: boolean;
  readonly emitTypeField: boolean;
  readonly declaredFields: readonly FieldSpec[];
}

type JsonObject = Record<string, unknown>;

class JsonNumber {
  constructor(readonly value: string) {}
}

function isObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function own(value: JsonObject, name: string): boolean {
  return Object.prototype.hasOwnProperty.call(value, name);
}

class StrictJsonParser {
  private index = 0;

  constructor(private readonly source: string) {}

  parse(): unknown {
    this.skipWhitespace();
    const value = this.parseValue();
    this.skipWhitespace();
    if (this.index !== this.source.length) this.fail("Unexpected trailing JSON content");
    return value;
  }

  private parseValue(): unknown {
    const current = this.source[this.index];
    if (current === "{") return this.parseObject();
    if (current === "[") return this.parseArray();
    if (current === "\"") return this.parseString();
    if (current === "t") return this.parseLiteral("true", true);
    if (current === "f") return this.parseLiteral("false", false);
    if (current === "n") return this.parseLiteral("null", null);
    if (current === "-" || (current !== undefined && current >= "0" && current <= "9")) {
      return this.parseNumber();
    }
    this.fail("Expected a JSON value");
  }

  private parseObject(): JsonObject {
    this.index++;
    const result = Object.create(null) as JsonObject;
    this.skipWhitespace();
    if (this.source[this.index] === "}") {
      this.index++;
      return result;
    }
    while (true) {
      if (this.source[this.index] !== "\"") this.fail("Expected a JSON object property name");
      const name = this.parseString();
      if (own(result, name)) this.fail(`Duplicate JSON object property ${JSON.stringify(name)}`);
      this.skipWhitespace();
      if (this.source[this.index] !== ":") this.fail("Expected ':' after a JSON property name");
      this.index++;
      this.skipWhitespace();
      result[name] = this.parseValue();
      this.skipWhitespace();
      const separator = this.source[this.index++];
      if (separator === "}") return result;
      if (separator !== ",") this.fail("Expected ',' or '}' in a JSON object");
      this.skipWhitespace();
    }
  }

  private parseArray(): unknown[] {
    this.index++;
    const result: unknown[] = [];
    this.skipWhitespace();
    if (this.source[this.index] === "]") {
      this.index++;
      return result;
    }
    while (true) {
      result.push(this.parseValue());
      this.skipWhitespace();
      const separator = this.source[this.index++];
      if (separator === "]") return result;
      if (separator !== ",") this.fail("Expected ',' or ']' in a JSON array");
      this.skipWhitespace();
    }
  }

  private parseString(): string {
    const start = this.index++;
    let escaped = false;
    while (this.index < this.source.length) {
      const current = this.source[this.index++]!;
      if (!escaped && current === "\"") {
        const raw = this.source.slice(start, this.index);
        try {
          return JSON.parse(raw) as string;
        } catch {
          this.fail("Malformed JSON string");
        }
      }
      if (!escaped && current.charCodeAt(0) < 0x20) this.fail("Unescaped control character in JSON string");
      if (!escaped && current === "\\") escaped = true;
      else escaped = false;
    }
    this.fail("Unterminated JSON string");
  }

  private parseNumber(): JsonNumber {
    const match = /^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/.exec(this.source.slice(this.index));
    if (match === null) this.fail("Malformed JSON number");
    this.index += match[0].length;
    return new JsonNumber(match[0]);
  }

  private parseLiteral<T>(text: string, value: T): T {
    if (!this.source.startsWith(text, this.index)) this.fail(`Malformed JSON literal`);
    this.index += text.length;
    return value;
  }

  private skipWhitespace(): void {
    while (/\s/.test(this.source[this.index] ?? "") && /[\t\n\r ]/.test(this.source[this.index] ?? "")) {
      this.index++;
    }
  }

  private fail(message: string): never {
    throw new SyntaxError(`${message} at offset ${this.index}.`);
  }
}

function parseJson(value: string | Uint8Array): unknown {
  const text = typeof value === "string" ? value : new TextDecoder("utf-8", { fatal: true }).decode(value);
  return new StrictJsonParser(text).parse();
}

function stringifyJson(value: unknown, indent?: number): string {
  if (indent !== undefined && (!Number.isInteger(indent) || indent < 0)) {
    throw new RangeError("indent must be a non-negative integer.");
  }
  const width = indent ?? 0;

  function write(current: unknown, level: number): string {
    if (current === null) return "null";
    if (typeof current === "string") return JSON.stringify(current);
    if (typeof current === "boolean") return current ? "true" : "false";
    if (typeof current === "bigint") return current.toString();
    if (typeof current === "number") {
      if (!Number.isFinite(current)) throw new TypeError("Non-finite values are not valid COGS JSON numbers.");
      return Object.is(current, -0) ? "0" : String(current);
    }
    if (current instanceof JsonNumber) return current.value;
    if (current instanceof CogsDecimal) return current.value;
    if (current instanceof CogsDuration) return current.milliseconds.value;
    if (Array.isArray(current)) {
      if (current.length === 0) return "[]";
      if (width === 0) return `[${current.map(item => write(item, level + 1)).join(",")}]`;
      const padding = " ".repeat(width * (level + 1));
      const closing = " ".repeat(width * level);
      return `[\n${padding}${current.map(item => write(item, level + 1)).join(`,\n${padding}`)}\n${closing}]`;
    }
    if (isObject(current)) {
      const entries = Object.entries(current).filter(([, item]) => item !== undefined);
      if (entries.length === 0) return "{}";
      const serialized = entries.map(([name, item]) => `${JSON.stringify(name)}${width === 0 ? ":" : ": "}${write(item, level + 1)}`);
      if (width === 0) return `{${serialized.join(",")}}`;
      const padding = " ".repeat(width * (level + 1));
      const closing = " ".repeat(width * level);
      return `{\n${padding}${serialized.join(`,\n${padding}`)}\n${closing}}`;
    }
    throw new TypeError(`Unsupported JSON value: ${String(current)}.`);
  }

  return write(value, 0);
}

function normalizeDecimal(value: string): string {
  const input = value.trim().replace(/^([+-]?)\./, (_match: string, sign: string) => `${sign}0.`);
  const match = /^([+-]?)(\d+)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$/.exec(input);
  if (match === null) throw new TypeError(`Invalid decimal value: ${JSON.stringify(value)}.`);
  const sign = match[1] === "-" ? "-" : "";
  const integer = match[2]!;
  const fraction = match[3] ?? "";
  const exponent = Number(match[4] ?? "0");
  if (!Number.isSafeInteger(exponent) || Math.abs(exponent) > 100_000) {
    throw new RangeError("Decimal exponent is too large.");
  }
  const digits = integer + fraction;
  const point = integer.length + exponent;
  let whole: string;
  let decimal: string;
  if (point <= 0) {
    whole = "0";
    decimal = "0".repeat(-point) + digits;
  } else if (point >= digits.length) {
    whole = digits + "0".repeat(point - digits.length);
    decimal = "";
  } else {
    whole = digits.slice(0, point);
    decimal = digits.slice(point);
  }
  whole = whole.replace(/^0+(?=\d)/, "");
  const isZero = /^0+$/.test(whole) && (decimal.length === 0 || /^0+$/.test(decimal));
  return `${isZero ? "" : sign}${whole}${decimal.length > 0 ? `.${decimal}` : ""}`;
}

interface DecimalParts {
  coefficient: bigint;
  scale: number;
}

function decimalParts(value: string): DecimalParts {
  const normalized = normalizeDecimal(value);
  const negative = normalized.startsWith("-");
  const unsigned = negative ? normalized.slice(1) : normalized;
  const [whole, fraction = ""] = unsigned.split(".");
  const coefficient = BigInt((whole ?? "0") + fraction);
  return { coefficient: negative ? -coefficient : coefficient, scale: fraction.length };
}

function decimalFromParts(parts: DecimalParts): string {
  const negative = parts.coefficient < 0n;
  let digits = (negative ? -parts.coefficient : parts.coefficient).toString();
  if (parts.scale > 0) digits = digits.padStart(parts.scale + 1, "0");
  const point = digits.length - parts.scale;
  const raw = parts.scale === 0 ? digits : `${digits.slice(0, point)}.${digits.slice(point)}`;
  return normalizeDecimal(`${negative ? "-" : ""}${raw}`);
}

function addDecimals(left: DecimalParts, right: DecimalParts): DecimalParts {
  const scale = Math.max(left.scale, right.scale);
  const leftCoefficient = left.coefficient * 10n ** BigInt(scale - left.scale);
  const rightCoefficient = right.coefficient * 10n ** BigInt(scale - right.scale);
  return { coefficient: leftCoefficient + rightCoefficient, scale };
}

/** An exact, string-backed decimal value. */
export class CogsDecimal {
  readonly value: string;

  constructor(value: string | number | bigint | CogsDecimal) {
    if (value instanceof CogsDecimal) this.value = value.value;
    else if (typeof value === "number") {
      if (!Number.isFinite(value)) throw new TypeError("Decimal values must be finite.");
      this.value = normalizeDecimal(String(value));
    } else this.value = normalizeDecimal(String(value));
  }

  toString(): string {
    return this.value;
  }
}

/** A duration represented as exact decimal milliseconds in JSON. */
export class CogsDuration {
  readonly milliseconds: CogsDecimal;

  constructor(milliseconds: string | number | bigint | CogsDecimal) {
    this.milliseconds = new CogsDecimal(milliseconds);
  }

  static fromXml(value: string): CogsDuration {
    const match = /^(-)?P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$/.exec(value);
    if (match === null || !match.slice(2).some(item => item !== undefined)) {
      throw new TypeError(`Invalid or unsupported XML duration: ${JSON.stringify(value)}.`);
    }
    let total: DecimalParts = { coefficient: 0n, scale: 0 };
    total = addDecimals(total, { coefficient: BigInt(match[2] ?? "0") * 86_400_000n, scale: 0 });
    total = addDecimals(total, { coefficient: BigInt(match[3] ?? "0") * 3_600_000n, scale: 0 });
    total = addDecimals(total, { coefficient: BigInt(match[4] ?? "0") * 60_000n, scale: 0 });
    const seconds = decimalParts(match[5] ?? "0");
    total = addDecimals(total, { coefficient: seconds.coefficient * 1000n, scale: seconds.scale });
    if (match[1] === "-") total.coefficient = -total.coefficient;
    return new CogsDuration(decimalFromParts(total));
  }

  toXml(): string {
    const parts = decimalParts(this.milliseconds.value);
    const negative = parts.coefficient < 0n;
    const coefficient = negative ? -parts.coefficient : parts.coefficient;
    if (coefficient === 0n) return "PT0S";
    const denominator = 10n ** BigInt(parts.scale + 3);
    const wholeSeconds = coefficient / denominator;
    const remainder = coefficient % denominator;
    const days = wholeSeconds / 86_400n;
    const afterDays = wholeSeconds % 86_400n;
    const hours = afterDays / 3_600n;
    const afterHours = afterDays % 3_600n;
    const minutes = afterHours / 60n;
    const seconds = afterHours % 60n;
    let secondText = seconds.toString();
    if (remainder !== 0n) {
      secondText += `.${remainder.toString().padStart(parts.scale + 3, "0").replace(/0+$/, "")}`;
    }
    let result = `${negative ? "-" : ""}P${days !== 0n ? `${days}D` : ""}`;
    if (hours !== 0n || minutes !== 0n || secondText !== "0") {
      result += `T${hours !== 0n ? `${hours}H` : ""}${minutes !== 0n ? `${minutes}M` : ""}${secondText !== "0" ? `${secondText}S` : ""}`;
    }
    return result;
  }
}

function validateTimezone(value: string | undefined): void {
  if (value === undefined || value === "Z") return;
  const match = /^[+-](\d{2}):(\d{2})$/.exec(value);
  if (match === null || Number(match[1]) > 23 || Number(match[2]) > 59) {
    throw new TypeError(`Invalid timezone: ${JSON.stringify(value)}.`);
  }
}

function isLeapYear(year: bigint): boolean {
  return year % 400n === 0n || (year % 4n === 0n && year % 100n !== 0n);
}

function validateDateParts(year: bigint, month: number, day: number): void {
  if (!Number.isInteger(month) || month < 1 || month > 12) throw new TypeError("Month must be between 1 and 12.");
  const days = [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (!Number.isInteger(day) || day < 1 || day > days[month - 1]!) throw new TypeError("Day is invalid for the month.");
}

function validateTimeParts(hour: number, minute: number, second: number): void {
  if (!Number.isInteger(hour) || hour < 0 || hour > 23
      || !Number.isInteger(minute) || minute < 0 || minute > 59
      || !Number.isInteger(second) || second < 0 || second > 59) {
    throw new TypeError("Invalid time value.");
  }
}

/** An ISO dateTime lexical value. */
export class CogsDateTime {
  constructor(readonly value: string) {
    const match = /^(-?\d{4,})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid dateTime: ${JSON.stringify(value)}.`);
    validateDateParts(BigInt(match[1]!), Number(match[2]), Number(match[3]));
    validateTimeParts(Number(match[4]), Number(match[5]), Number(match[6]));
    validateTimezone(match[7]);
  }

  toString(): string { return this.value; }
}

/** An ISO date lexical value. */
export class CogsDateOnly {
  constructor(readonly value: string) {
    const match = /^(-?\d{4,})-(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid date: ${JSON.stringify(value)}.`);
    validateDateParts(BigInt(match[1]!), Number(match[2]), Number(match[3]));
    validateTimezone(match[4]);
  }

  toString(): string { return this.value; }
}

/** An ISO time lexical value. */
export class CogsTime {
  constructor(readonly value: string) {
    const match = /^(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid time: ${JSON.stringify(value)}.`);
    validateTimeParts(Number(match[1]), Number(match[2]), Number(match[3]));
    validateTimezone(match[4]);
  }

  toString(): string { return this.value; }
}

function yearText(year: bigint): string {
  const negative = year < 0n;
  const digits = (negative ? -year : year).toString().padStart(4, "0");
  return `${negative ? "-" : ""}${digits}`;
}

function parseSmallInteger(value: unknown, name: string): number {
  const result = parseInteger(value, "int");
  if (typeof result !== "number") throw new TypeError(`${name} must be a number.`);
  return result;
}

function requireObjectKeys(value: unknown, required: readonly string[], optional: readonly string[] = []): JsonObject {
  if (!isObject(value)) throw new TypeError("Expected an object.");
  const allowed = new Set([...required, ...optional]);
  const unknown = Object.keys(value).filter(name => !allowed.has(name));
  if (unknown.length > 0) throw new TypeError(`Unknown fields: ${unknown.sort().join(", ")}.`);
  for (const name of required) if (!own(value, name)) throw new TypeError(`Missing field ${name}.`);
  return value;
}

export class GYearMonth {
  readonly year: bigint;
  readonly month: number;
  readonly timezone: string | undefined;

  constructor(year: bigint | number, month: number, timezone?: string) {
    this.year = BigInt(year);
    this.month = month;
    this.timezone = timezone;
    if (!Number.isInteger(month) || month < 1 || month > 12) throw new TypeError("Month must be between 1 and 12.");
    validateTimezone(timezone);
  }

  toObject(): JsonObject {
    return { Year: this.year, Month: this.month, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) };
  }

  static fromObject(value: unknown): GYearMonth {
    const raw = requireObjectKeys(value, ["Year", "Month"], ["Timezone"]);
    return new GYearMonth(parseInteger(raw.Year, "gYear") as bigint, parseSmallInteger(raw.Month, "Month"), optionalString(raw.Timezone));
  }

  toXml(): string { return `${yearText(this.year)}-${String(this.month).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GYearMonth {
    const match = /^(-?\d{4,})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gYearMonth: ${JSON.stringify(value)}.`);
    return new GYearMonth(BigInt(match[1]!), Number(match[2]), match[3]);
  }
}

export class GYear {
  readonly year: bigint;
  readonly timezone: string | undefined;

  constructor(year: bigint | number, timezone?: string) {
    this.year = BigInt(year);
    this.timezone = timezone;
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Year: this.year, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GYear {
    const raw = requireObjectKeys(value, ["Year"], ["Timezone"]);
    return new GYear(parseInteger(raw.Year, "gYear") as bigint, optionalString(raw.Timezone));
  }

  toXml(): string { return `${yearText(this.year)}${this.timezone ?? ""}`; }

  static fromXml(value: string): GYear {
    const match = /^(-?\d{4,})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gYear: ${JSON.stringify(value)}.`);
    return new GYear(BigInt(match[1]!), match[2]);
  }
}

export class GMonthDay {
  constructor(readonly month: number, readonly day: number, readonly timezone?: string) {
    validateDateParts(2000n, month, day);
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Month: this.month, Day: this.day, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GMonthDay {
    const raw = requireObjectKeys(value, ["Month", "Day"], ["Timezone"]);
    return new GMonthDay(parseSmallInteger(raw.Month, "Month"), parseSmallInteger(raw.Day, "Day"), optionalString(raw.Timezone));
  }

  toXml(): string { return `--${String(this.month).padStart(2, "0")}-${String(this.day).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GMonthDay {
    const match = /^--(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gMonthDay: ${JSON.stringify(value)}.`);
    return new GMonthDay(Number(match[1]), Number(match[2]), match[3]);
  }
}

export class GMonth {
  constructor(readonly month: number, readonly timezone?: string) {
    if (!Number.isInteger(month) || month < 1 || month > 12) throw new TypeError("Month must be between 1 and 12.");
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Month: this.month, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GMonth {
    const raw = requireObjectKeys(value, ["Month"], ["Timezone"]);
    return new GMonth(parseSmallInteger(raw.Month, "Month"), optionalString(raw.Timezone));
  }

  toXml(): string { return `--${String(this.month).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GMonth {
    const match = /^--(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gMonth: ${JSON.stringify(value)}.`);
    return new GMonth(Number(match[1]), match[2]);
  }
}

export class GDay {
  constructor(readonly day: number, readonly timezone?: string) {
    if (!Number.isInteger(day) || day < 1 || day > 31) throw new TypeError("Day must be between 1 and 31.");
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Day: this.day, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GDay {
    const raw = requireObjectKeys(value, ["Day"], ["Timezone"]);
    return new GDay(parseSmallInteger(raw.Day, "Day"), optionalString(raw.Timezone));
  }

  toXml(): string { return `---${String(this.day).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GDay {
    const match = /^---(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gDay: ${JSON.stringify(value)}.`);
    return new GDay(Number(match[1]), match[2]);
  }
}

export class LangString {
  constructor(readonly language: string, readonly value: string) {
    if (typeof language !== "string" || typeof value !== "string") throw new TypeError("LangString values must be strings.");
  }

  toObject(): JsonObject { return { "@language": this.language, "@value": this.value }; }

  static fromObject(value: unknown): LangString {
    const raw = requireObjectKeys(value, ["@language", "@value"]);
    if (typeof raw["@language"] !== "string" || typeof raw["@value"] !== "string") {
      throw new TypeError("langString fields must be strings.");
    }
    return new LangString(raw["@language"], raw["@value"]);
  }
}

export type CogsDateKind = "DateTime" | "Date" | "GYearMonth" | "GYear" | "Duration";
export type CogsDateValue = CogsDateTime | CogsDateOnly | GYearMonth | GYear | CogsDuration;

export class CogsDate {
  constructor(readonly kind: CogsDateKind, readonly value: CogsDateValue) {
    const valid = (kind === "DateTime" && value instanceof CogsDateTime)
      || (kind === "Date" && value instanceof CogsDateOnly)
      || (kind === "GYearMonth" && value instanceof GYearMonth)
      || (kind === "GYear" && value instanceof GYear)
      || (kind === "Duration" && value instanceof CogsDuration);
    if (!valid) throw new TypeError(`${value.constructor.name} is not valid for CogsDate ${kind}.`);
  }

  static dateTime(value: string | CogsDateTime): CogsDate { return new CogsDate("DateTime", value instanceof CogsDateTime ? value : new CogsDateTime(value)); }
  static date(value: string | CogsDateOnly): CogsDate { return new CogsDate("Date", value instanceof CogsDateOnly ? value : new CogsDateOnly(value)); }
  static gYearMonth(value: GYearMonth): CogsDate { return new CogsDate("GYearMonth", value); }
  static gYear(value: GYear): CogsDate { return new CogsDate("GYear", value); }
  static duration(value: CogsDuration): CogsDate { return new CogsDate("Duration", value); }

  toObject(): JsonObject {
    if (this.value instanceof CogsDateTime || this.value instanceof CogsDateOnly) return { [this.kind]: this.value.value };
    if (this.value instanceof GYearMonth || this.value instanceof GYear) return { [this.kind]: this.value.toObject() };
    return { Duration: this.value };
  }

  static fromObject(value: unknown): CogsDate {
    const raw = requireObjectKeys(value, [], ["DateTime", "Date", "GYearMonth", "GYear", "Duration"]);
    const keys = Object.keys(raw);
    if (keys.length !== 1) throw new TypeError("cogsDate requires exactly one value.");
    const kind = keys[0] as CogsDateKind;
    const item = raw[kind];
    switch (kind) {
      case "DateTime": return CogsDate.dateTime(requireString(item, "DateTime"));
      case "Date": return CogsDate.date(requireString(item, "Date"));
      case "GYearMonth": return CogsDate.gYearMonth(GYearMonth.fromObject(item));
      case "GYear": return CogsDate.gYear(GYear.fromObject(item));
      case "Duration": return CogsDate.duration(parseDuration(item));
      default: throw new TypeError(`Unknown cogsDate kind ${kind}.`);
    }
  }

  toXml(): string {
    if (this.value instanceof CogsDateTime || this.value instanceof CogsDateOnly) return this.value.value;
    return this.value.toXml();
  }

  static fromXml(value: string): CogsDate {
    if (/^-?P/.test(value)) return CogsDate.duration(CogsDuration.fromXml(value));
    if (value.includes("T")) return CogsDate.dateTime(value);
    if (/^-?\d{4,}-\d{2}-\d{2}/.test(value)) return CogsDate.date(value);
    if (/^-?\d{4,}-\d{2}/.test(value)) return CogsDate.gYearMonth(GYearMonth.fromXml(value));
    return CogsDate.gYear(GYear.fromXml(value));
  }
}

function optionalString(value: unknown): string | undefined {
  if (value === undefined) return undefined;
  if (typeof value !== "string") throw new TypeError("Expected a string.");
  return value;
}

function requireString(value: unknown, name: string): string {
  if (typeof value !== "string") throw new TypeError(`${name} must be a string.`);
  return value;
}

function numericLexeme(value: unknown, name: string): string {
  if (value instanceof JsonNumber) return value.value;
  if (value instanceof CogsDecimal) return value.value;
  if (typeof value === "bigint") return value.toString();
  if (typeof value === "number" && Number.isFinite(value)) return String(value);
  throw new TypeError(`${name} must be a JSON number.`);
}

function parseInteger(value: unknown, typeName: string): number | bigint {
  const lexical = numericLexeme(value, typeName);
  if (!/^-?(?:0|[1-9]\d*)$/.test(lexical)) throw new TypeError(`${typeName} must be an integer.`);
  const result = BigInt(lexical);
  switch (typeName.toLowerCase()) {
    case "int":
      if (result < -2_147_483_648n || result > 2_147_483_647n) throw new RangeError("int is outside its XSD range.");
      return Number(result);
    case "nonpositiveinteger": if (result > 0n) throw new RangeError("nonPositiveInteger must be <= 0."); break;
    case "negativeinteger": if (result >= 0n) throw new RangeError("negativeInteger must be < 0."); break;
    case "long": if (result < -9_223_372_036_854_775_808n || result > 9_223_372_036_854_775_807n) throw new RangeError("long is outside its XSD range."); break;
    case "nonnegativeinteger": if (result < 0n) throw new RangeError("nonNegativeInteger must be >= 0."); break;
    case "unsignedlong": if (result < 0n || result > 18_446_744_073_709_551_615n) throw new RangeError("unsignedLong is outside its XSD range."); break;
    case "positiveinteger": if (result <= 0n) throw new RangeError("positiveInteger must be > 0."); break;
    case "gyear": break;
    default: throw new TypeError(`Unknown integer type ${typeName}.`);
  }
  return result;
}

function parseDuration(value: unknown): CogsDuration {
  if (value instanceof CogsDuration) return value;
  return new CogsDuration(numericLexeme(value, "duration"));
}

function serializeSimpleObject(typeName: string, value: unknown): unknown {
  const lower = typeName.toLowerCase();
  if (lower === "string" || lower === "language" || lower === "anyuri") return requireString(value, typeName);
  if (lower === "boolean") {
    if (typeof value !== "boolean") throw new TypeError("boolean values must be boolean.");
    return value;
  }
  if (lower === "int") return parseInteger(value, typeName);
  if (["nonpositiveinteger", "negativeinteger", "long", "nonnegativeinteger", "unsignedlong", "positiveinteger"].includes(lower)) {
    return parseInteger(value, typeName);
  }
  if (lower === "float" || lower === "double") {
    if (typeof value !== "number" || !Number.isFinite(value)) throw new TypeError(`${typeName} must be a finite number.`);
    return value;
  }
  if (lower === "decimal") {
    if (!(value instanceof CogsDecimal)) throw new TypeError("decimal values require CogsDecimal.");
    return value;
  }
  if (lower === "duration") {
    if (!(value instanceof CogsDuration)) throw new TypeError("duration values require CogsDuration.");
    return value;
  }
  if (lower === "datetime") return value instanceof CogsDateTime ? value.value : (() => { throw new TypeError("dateTime values require CogsDateTime."); })();
  if (lower === "date") return value instanceof CogsDateOnly ? value.value : (() => { throw new TypeError("date values require CogsDateOnly."); })();
  if (lower === "time") return value instanceof CogsTime ? value.value : (() => { throw new TypeError("time values require CogsTime."); })();
  if (lower === "gyearmonth" && value instanceof GYearMonth) return value.toObject();
  if (lower === "gyear" && value instanceof GYear) return value.toObject();
  if (lower === "gmonthday" && value instanceof GMonthDay) return value.toObject();
  if (lower === "gmonth" && value instanceof GMonth) return value.toObject();
  if (lower === "gday" && value instanceof GDay) return value.toObject();
  if (lower === "langstring" && value instanceof LangString) return value.toObject();
  if (lower === "cogsdate" && value instanceof CogsDate) return value.toObject();
  throw new TypeError(`Invalid ${typeName} value.`);
}

function deserializeSimpleObject(typeName: string, value: unknown): unknown {
  const lower = typeName.toLowerCase();
  if (lower === "string" || lower === "language" || lower === "anyuri") return requireString(value, typeName);
  if (lower === "boolean") {
    if (typeof value !== "boolean") throw new TypeError("boolean values must be boolean.");
    return value;
  }
  if (lower === "int" || ["nonpositiveinteger", "negativeinteger", "long", "nonnegativeinteger", "unsignedlong", "positiveinteger"].includes(lower)) {
    return parseInteger(value, typeName);
  }
  if (lower === "float" || lower === "double") {
    const result = Number(numericLexeme(value, typeName));
    if (!Number.isFinite(result)) throw new TypeError(`${typeName} must be finite.`);
    return result;
  }
  if (lower === "decimal") return value instanceof CogsDecimal ? value : new CogsDecimal(numericLexeme(value, "decimal"));
  if (lower === "duration") return parseDuration(value);
  if (lower === "datetime") return value instanceof CogsDateTime ? value : new CogsDateTime(requireString(value, typeName));
  if (lower === "date") return value instanceof CogsDateOnly ? value : new CogsDateOnly(requireString(value, typeName));
  if (lower === "time") return value instanceof CogsTime ? value : new CogsTime(requireString(value, typeName));
  if (lower === "gyearmonth") return value instanceof GYearMonth ? value : GYearMonth.fromObject(value);
  if (lower === "gyear") return value instanceof GYear ? value : GYear.fromObject(value);
  if (lower === "gmonthday") return value instanceof GMonthDay ? value : GMonthDay.fromObject(value);
  if (lower === "gmonth") return value instanceof GMonth ? value : GMonth.fromObject(value);
  if (lower === "gday") return value instanceof GDay ? value : GDay.fromObject(value);
  if (lower === "langstring") return value instanceof LangString ? value : LangString.fromObject(value);
  if (lower === "cogsdate") return value instanceof CogsDate ? value : CogsDate.fromObject(value);
  throw new TypeError(`Unknown simple type ${typeName}.`);
}

const ITEM_TYPE_REGISTRY = new Map<string, CogsConstructor<CogsItem>>();
const TYPE_REGISTRY = new Map<string, CogsConstructor>();

function registerTypes(
  itemEntries: readonly (readonly [string, CogsConstructor<CogsItem>])[],
  typeEntries: readonly (readonly [string, CogsConstructor])[],
): void {
  for (const [name, value] of itemEntries) ITEM_TYPE_REGISTRY.set(name, value);
  for (const [name, value] of typeEntries) TYPE_REGISTRY.set(name, value);
}

function constructorOf(value: CogsValue): CogsConstructor {
  return value.constructor as unknown as CogsConstructor;
}

function createInstance<T extends CogsValue>(constructor: CogsConstructor<T>): T {
  return new (constructor as unknown as new () => T)();
}

function isAssignable(actual: CogsConstructor, expected: CogsConstructor): boolean {
  return actual === expected || actual.prototype instanceof (expected as unknown as new () => CogsValue);
}

function fieldsFor(constructor: CogsConstructor): readonly FieldSpec[] {
  const groups: (readonly FieldSpec[])[] = [];
  let current: unknown = constructor;
  while (typeof current === "function" && current !== CogsValue && current !== CogsItem) {
    if (Object.prototype.hasOwnProperty.call(current, "declaredFields")) {
      groups.push((current as unknown as CogsConstructor).declaredFields);
    }
    current = Object.getPrototypeOf(current);
  }
  return groups.reverse().flat();
}

function fieldMap(constructor: CogsConstructor): ReadonlyMap<string, FieldSpec> {
  return new Map(fieldsFor(constructor).map(field => [field.cogsName, field]));
}

function typeForName(typeName: string): CogsConstructor {
  const result = TYPE_REGISTRY.get(typeName);
  if (result === undefined) throw new TypeError(`Unknown COGS type ${typeName}.`);
  return result;
}

class Context {
  readonly itemsByKey = new Map<string, CogsItem>();
  readonly definedKeys = new Set<string>();

  key(typeName: string, raw: JsonObject): string {
    const values: unknown[] = [];
    for (const field of IDENTIFICATION_FIELDS) {
      if (!own(raw, field.cogsName)) throw new TypeError(`Reference is missing identification field ${field.cogsName}.`);
      values.push(raw[field.cogsName]);
    }
    return `${typeName}|${stringifyJson(values)}`;
  }

  resolveReference(rawValue: unknown, expectedType?: string, allowSubtypes = true): CogsItem {
    if (!isObject(rawValue)) throw new TypeError("Item references must be objects.");
    const raw = rawValue;
    const allowed = new Set(["$type", ...IDENTIFICATION_FIELDS.map(field => field.cogsName)]);
    const unknown = Object.keys(raw).filter(name => !allowed.has(name));
    if (unknown.length > 0) throw new TypeError(`Unknown reference fields: ${unknown.sort().join(", ")}.`);
    if (typeof raw.$type !== "string") throw new TypeError("Item references require a string $type.");
    const actual = ITEM_TYPE_REGISTRY.get(raw.$type);
    if (actual === undefined) throw new TypeError(`Unknown item type ${raw.$type}.`);
    if (actual.isAbstract) throw new TypeError(`Abstract item type cannot be instantiated: ${raw.$type}.`);
    if (expectedType !== undefined) {
      const expected = ITEM_TYPE_REGISTRY.get(expectedType);
      if (expected === undefined) throw new TypeError(`Unknown declared item type ${expectedType}.`);
      if (!isAssignable(actual, expected)) {
        throw new TypeError(`${raw.$type} is not assignable to ${expectedType}.`);
      }
    }
    const key = this.key(raw.$type, raw);
    let result = this.itemsByKey.get(key);
    if (result === undefined) {
      result = createInstance(actual);
      const fields = fieldMap(actual);
      for (const identity of IDENTIFICATION_FIELDS) {
        const field = fields.get(identity.cogsName);
        if (field === undefined) throw new TypeError(`Item ${raw.$type} has no ${identity.cogsName} field.`);
        result[identity.attributeName] = deserializeSimpleObject(field.typeName, raw[identity.cogsName]);
      }
      this.itemsByKey.set(key, result);
    }
    return result;
  }

  loadItem(value: unknown): CogsItem {
    if (!isObject(value) || typeof value.$type !== "string") throw new TypeError("Serialized items require a string $type discriminator.");
    const reference: JsonObject = { $type: value.$type };
    for (const field of IDENTIFICATION_FIELDS) if (own(value, field.cogsName)) reference[field.cogsName] = value[field.cogsName];
    const result = this.resolveReference(reference, value.$type);
    const key = this.key(value.$type, value);
    if (this.definedKeys.has(key)) throw new TypeError(`Duplicate full item definition: ${value.$type}.`);
    this.definedKeys.add(key);
    populateFromObject(result, value, this);
    return result;
  }
}

function serializeFieldObject(value: unknown, field: FieldSpec, context: Context): unknown {
  if (field.many) {
    if (!Array.isArray(value)) throw new TypeError(`${field.cogsName} must be an array.`);
    return value.map(item => serializeSingleObject(item, field, context));
  }
  return serializeSingleObject(value, field, context);
}

function serializeSingleObject(value: unknown, field: FieldSpec, context: Context): unknown {
  if (field.kind === "simple") return serializeSimpleObject(field.typeName, value);
  if (!(value instanceof CogsValue)) throw new TypeError(`${field.cogsName} requires a COGS value.`);
  const actual = constructorOf(value);
  if (field.kind === "item") {
    if (!(value instanceof CogsItem)) throw new TypeError(`${field.cogsName} requires an item reference.`);
    const expected = ITEM_TYPE_REGISTRY.get(field.typeName);
    if (expected === undefined || !isAssignable(actual, expected)) {
      throw new TypeError(`Invalid item type for ${field.cogsName}.`);
    }
    return value.toReferenceObject();
  }
  const expected = TYPE_REGISTRY.get(field.typeName);
  if (expected === undefined || !isAssignable(actual, expected) || (!field.allowSubtypes && actual !== expected)) {
    throw new TypeError(`Invalid object type for ${field.cogsName}.`);
  }
  return valueToObject(value, context);
}

function deserializeFieldObject(value: unknown, field: FieldSpec, context: Context): unknown {
  if (field.many) {
    if (!Array.isArray(value)) throw new TypeError(`${field.cogsName} must be an array.`);
    return value.map(item => deserializeSingleObject(item, field, context));
  }
  return deserializeSingleObject(value, field, context);
}

function deserializeSingleObject(value: unknown, field: FieldSpec, context: Context): unknown {
  if (field.kind === "simple") return deserializeSimpleObject(field.typeName, value);
  if (field.kind === "item") return context.resolveReference(value, field.typeName, field.allowSubtypes);
  if (!isObject(value)) throw new TypeError(`${field.cogsName} must be an object.`);
  let target = TYPE_REGISTRY.get(field.typeName);
  if (target === undefined) throw new TypeError(`Unknown declared type ${field.typeName}.`);
  if (own(value, "$type")) {
    if (typeof value.$type !== "string") throw new TypeError("$type must be a string.");
    const candidate = typeForName(value.$type);
    if (!isAssignable(candidate, target) || candidate.isAbstract || (!field.allowSubtypes && candidate !== target)) {
      throw new TypeError(`${value.$type} is not allowed for ${field.cogsName}.`);
    }
    target = candidate;
  }
  if (target.isAbstract) throw new TypeError(`Abstract type ${target.cogsType} requires $type.`);
  const result = createInstance(target);
  populateFromObject(result, value, context);
  return result;
}

function populateFromObject(target: CogsValue, value: unknown, context: Context): void {
  if (!isObject(value)) throw new TypeError(`${constructorOf(target).cogsType} must be an object.`);
  const constructor = constructorOf(target);
  const fields = fieldMap(constructor);
  const allowed = new Set(fields.keys());
  if (constructor.isItem || constructor.emitTypeField) allowed.add("$type");
  const unknown = Object.keys(value).filter(name => !allowed.has(name));
  if (unknown.length > 0) throw new TypeError(`Unknown fields for ${constructor.cogsType}: ${unknown.sort().join(", ")}.`);
  if (own(value, "$type") && value.$type !== constructor.cogsType) {
    throw new TypeError(`Expected $type ${constructor.cogsType}, got ${String(value.$type)}.`);
  }
  for (const [wireName, field] of fields) {
    if (own(value, wireName)) target[field.attributeName] = deserializeFieldObject(value[wireName], field, context);
  }
}

function valueToObject(value: CogsValue, context: Context): JsonObject {
  const constructor = constructorOf(value);
  if (constructor.isAbstract) throw new TypeError(`Abstract type cannot be serialized: ${constructor.cogsType}.`);
  const result: JsonObject = Object.create(null) as JsonObject;
  if (constructor.isItem || constructor.emitTypeField) result.$type = constructor.cogsType;
  for (const field of fieldsFor(constructor)) {
    const fieldValue = value[field.attributeName];
    if (fieldValue === undefined || (field.many && Array.isArray(fieldValue) && fieldValue.length === 0)) continue;
    result[field.cogsName] = serializeFieldObject(fieldValue, field, context);
  }
  return result;
}

export class CogsValue {
  static readonly cogsType: string = "";
  static readonly isItem: boolean = false;
  static readonly isAbstract: boolean = true;
  static readonly emitTypeField: boolean = false;
  static readonly declaredFields: readonly FieldSpec[] = [];

  [name: string]: unknown;

  toObject(): JsonObject { return valueToObject(this, new Context()); }

  static fromObject<T extends CogsValue>(this: CogsConstructor<T>, value: unknown): T {
    const context = new Context();
    if (this.isItem) {
      const result = context.loadItem(value);
      if (!isAssignable(constructorOf(result), this)) throw new TypeError(`${constructorOf(result).cogsType} is not assignable to ${this.cogsType}.`);
      return result as unknown as T;
    }
    if (!isObject(value)) throw new TypeError(`${this.cogsType} must be an object.`);
    let target: CogsConstructor = this;
    if (own(value, "$type")) {
      if (typeof value.$type !== "string") throw new TypeError("$type must be a string.");
      const candidate = typeForName(value.$type);
      if (!isAssignable(candidate, this) || candidate.isAbstract) throw new TypeError(`${value.$type} is not assignable to ${this.cogsType}.`);
      target = candidate;
    }
    if (target.isAbstract) throw new TypeError(`Abstract type ${target.cogsType} requires $type.`);
    const result = createInstance(target);
    populateFromObject(result, value, context);
    return result as T;
  }

  toJson(options: { readonly indent?: number } = {}): string { return stringifyJson(this.toObject(), options.indent); }

  static fromJson<T extends CogsValue>(this: CogsConstructor<T>, value: string | Uint8Array): T {
    return CogsValue.fromObject.call(this, parseJson(value)) as T;
  }

  toElement(elementName?: string): Element {
    const document = createDocument(elementName ?? constructorOf(this).cogsType);
    return toElementWithContext(this, document.documentElement!, new Context());
  }

  static fromElement<T extends CogsValue>(this: CogsConstructor<T>, element: Element): T {
    requireTargetElement(element);
    const target = targetFromElement(this, element, true);
    const result = createInstance(target);
    populateFromElement(result, element, new Context());
    return result as T;
  }

  toXml(elementName?: string, options: { readonly xmlDeclaration?: boolean } = {}): string {
    const element = this.toElement(elementName);
    return serializeDocument(element.ownerDocument!, options.xmlDeclaration ?? false);
  }

  static fromXml<T extends CogsValue>(this: CogsConstructor<T>, value: string | Uint8Array): T {
    return CogsValue.fromElement.call(this, parseXml(value).documentElement!) as T;
  }
}

export class CogsItem extends CogsValue {
  static override readonly isItem: boolean = true;

  toReferenceObject(): JsonObject {
    const constructor = constructorOf(this);
    const fields = fieldMap(constructor);
    const result: JsonObject = { $type: constructor.cogsType };
    for (const identity of IDENTIFICATION_FIELDS) {
      const field = fields.get(identity.cogsName);
      if (field === undefined) throw new TypeError(`${constructor.cogsType} has no ${identity.cogsName} field.`);
      const value = this[identity.attributeName];
      if (value === undefined) throw new TypeError(`Reference field ${identity.cogsName} is not set.`);
      result[identity.cogsName] = serializeSimpleObject(field.typeName, value);
    }
    return result;
  }
}

function createDocument(rootName: string): Document {
  const document = new DOMImplementation().createDocument(TARGET_NAMESPACE, `${NAMESPACE_PREFIX}:${rootName}`, null);
  document.documentElement!.setAttributeNS(XMLNS_NAMESPACE, `xmlns:${NAMESPACE_PREFIX}`, TARGET_NAMESPACE);
  document.documentElement!.setAttributeNS(XMLNS_NAMESPACE, "xmlns:xsi", XSI_NAMESPACE);
  return document;
}

function createElement(document: Document, name: string): Element {
  return document.createElementNS(TARGET_NAMESPACE, `${NAMESPACE_PREFIX}:${name}`);
}

function allowedAttributes(element: Element, extra: readonly (readonly [string | null, string])[] = []): void {
  const allowed = new Set(extra.map(([namespace, name]) => `${namespace ?? ""}|${name}`));
  for (let index = 0; index < element.attributes.length; index++) {
    const attribute = element.attributes.item(index)!;
    if (attribute.namespaceURI === XMLNS_NAMESPACE) continue;
    if (!allowed.has(`${attribute.namespaceURI ?? ""}|${attribute.localName ?? attribute.name}`)) {
      throw new TypeError(`Unknown XML attribute ${attribute.name} on ${element.localName ?? element.tagName}.`);
    }
  }
}

function childElements(element: Element, allowText = false): Element[] {
  const result: Element[] = [];
  for (let index = 0; index < element.childNodes.length; index++) {
    const child = element.childNodes.item(index)!;
    if (child.nodeType === child.ELEMENT_NODE) result.push(child as Element);
    else if (child.nodeType === child.TEXT_NODE || child.nodeType === child.CDATA_SECTION_NODE) {
      if (!allowText && (child.nodeValue ?? "").trim().length > 0) {
        throw new TypeError(`Unexpected XML text in ${element.localName ?? element.tagName}.`);
      }
    } else if (child.nodeType !== child.COMMENT_NODE) {
      throw new TypeError(`Unexpected XML node in ${element.localName ?? element.tagName}.`);
    }
  }
  return result;
}

function elementText(element: Element): string {
  if (childElements(element, true).length > 0) throw new TypeError(`${element.localName ?? element.tagName} cannot contain child elements.`);
  let result = "";
  for (let index = 0; index < element.childNodes.length; index++) {
    const child = element.childNodes.item(index)!;
    if (child.nodeType === child.TEXT_NODE || child.nodeType === child.CDATA_SECTION_NODE) result += child.nodeValue ?? "";
  }
  return result;
}

function requireTargetElement(element: Element, expectedName?: string): string {
  if (element.namespaceURI !== TARGET_NAMESPACE) throw new TypeError(`Unexpected XML namespace on ${element.tagName}.`);
  const name = element.localName ?? element.tagName.split(":").at(-1)!;
  if (expectedName !== undefined && name !== expectedName) throw new TypeError(`Expected ${expectedName}, got ${name}.`);
  return name;
}

function serializeSimpleXml(typeName: string, value: unknown, element: Element): void {
  const lower = typeName.toLowerCase();
  if (lower === "langstring") {
    if (!(value instanceof LangString)) throw new TypeError("langString values require LangString.");
    element.setAttributeNS(XML_NAMESPACE, "xml:lang", value.language);
    element.appendChild(element.ownerDocument!.createTextNode(value.value));
    return;
  }
  let text: string;
  if (lower === "boolean") {
    if (typeof value !== "boolean") throw new TypeError("boolean values must be boolean.");
    text = value ? "true" : "false";
  } else if (lower === "decimal") {
    if (!(value instanceof CogsDecimal)) throw new TypeError("decimal values require CogsDecimal.");
    text = value.value;
  } else if (lower === "duration") {
    if (!(value instanceof CogsDuration)) throw new TypeError("duration values require CogsDuration.");
    text = value.toXml();
  } else if (lower === "datetime") {
    if (!(value instanceof CogsDateTime)) throw new TypeError("dateTime values require CogsDateTime.");
    text = value.value;
  } else if (lower === "date") {
    if (!(value instanceof CogsDateOnly)) throw new TypeError("date values require CogsDateOnly.");
    text = value.value;
  } else if (lower === "time") {
    if (!(value instanceof CogsTime)) throw new TypeError("time values require CogsTime.");
    text = value.value;
  } else if (lower === "gyearmonth" && value instanceof GYearMonth) text = value.toXml();
  else if (lower === "gyear" && value instanceof GYear) text = value.toXml();
  else if (lower === "gmonthday" && value instanceof GMonthDay) text = value.toXml();
  else if (lower === "gmonth" && value instanceof GMonth) text = value.toXml();
  else if (lower === "gday" && value instanceof GDay) text = value.toXml();
  else if (lower === "cogsdate" && value instanceof CogsDate) text = value.toXml();
  else if (lower === "int" || ["nonpositiveinteger", "negativeinteger", "long", "nonnegativeinteger", "unsignedlong", "positiveinteger"].includes(lower)) {
    text = String(parseInteger(value, typeName));
  } else if (lower === "float" || lower === "double") {
    if (typeof value !== "number" || !Number.isFinite(value)) throw new TypeError(`${typeName} must be finite.`);
    text = String(value);
  } else text = requireString(value, typeName);
  element.appendChild(element.ownerDocument!.createTextNode(text));
}

function deserializeSimpleXml(typeName: string, element: Element): unknown {
  const lower = typeName.toLowerCase();
  if (lower === "langstring") {
    allowedAttributes(element, [[XML_NAMESPACE, "lang"]]);
    const language = element.getAttributeNS(XML_NAMESPACE, "lang");
    if (language === null) throw new TypeError("langString requires xml:lang.");
    return new LangString(language, elementText(element));
  }
  allowedAttributes(element);
  const text = elementText(element).trim();
  if (lower === "boolean") {
    if (text === "true" || text === "1") return true;
    if (text === "false" || text === "0") return false;
    throw new TypeError(`Invalid boolean lexical value ${JSON.stringify(text)}.`);
  }
  if (lower === "decimal") return new CogsDecimal(text);
  if (lower === "duration") return CogsDuration.fromXml(text);
  if (lower === "datetime") return new CogsDateTime(text);
  if (lower === "date") return new CogsDateOnly(text);
  if (lower === "time") return new CogsTime(text);
  if (lower === "gyearmonth") return GYearMonth.fromXml(text);
  if (lower === "gyear") return GYear.fromXml(text);
  if (lower === "gmonthday") return GMonthDay.fromXml(text);
  if (lower === "gmonth") return GMonth.fromXml(text);
  if (lower === "gday") return GDay.fromXml(text);
  if (lower === "cogsdate") return CogsDate.fromXml(text);
  if (lower === "int" || ["nonpositiveinteger", "negativeinteger", "long", "nonnegativeinteger", "unsignedlong", "positiveinteger"].includes(lower)) {
    return parseInteger(new JsonNumber(text), typeName);
  }
  if (lower === "float" || lower === "double") {
    const result = Number(text);
    if (!Number.isFinite(result)) throw new TypeError(`${typeName} must be finite.`);
    return result;
  }
  return text;
}

function toElementWithContext(value: CogsValue, element: Element, context: Context, declaredType?: string, allowSubtypes = false): Element {
  const actual = constructorOf(value);
  if (actual.isAbstract) throw new TypeError(`Abstract type cannot be serialized: ${actual.cogsType}.`);
  if (declaredType !== undefined && actual.cogsType !== declaredType) {
    const declared = typeForName(declaredType);
    if (!allowSubtypes || !isAssignable(actual, declared)) {
      throw new TypeError(`${actual.cogsType} is not allowed where ${declaredType} is declared.`);
    }
    element.setAttributeNS(XSI_NAMESPACE, "xsi:type", `${NAMESPACE_PREFIX}:${actual.cogsType}`);
  }
  for (const field of fieldsFor(actual)) {
    const fieldValue = value[field.attributeName];
    if (fieldValue === undefined || (field.many && Array.isArray(fieldValue) && fieldValue.length === 0)) continue;
    const values = field.many ? fieldValue as unknown[] : [fieldValue];
    if (!Array.isArray(values)) throw new TypeError(`${field.cogsName} must be an array.`);
    for (const item of values) element.appendChild(serializeFieldXml(item, field, context, element.ownerDocument!));
  }
  return element;
}

function serializeFieldXml(value: unknown, field: FieldSpec, context: Context, document: Document): Element {
  const element = createElement(document, field.cogsName);
  if (field.kind === "simple") {
    serializeSimpleXml(field.typeName, value, element);
    return element;
  }
  if (!(value instanceof CogsValue)) throw new TypeError(`${field.cogsName} requires a COGS value.`);
  const actual = constructorOf(value);
  if (field.kind === "item") {
    if (!(value instanceof CogsItem)) throw new TypeError(`${field.cogsName} requires an item reference.`);
    const expected = ITEM_TYPE_REGISTRY.get(field.typeName);
    if (expected === undefined || !isAssignable(actual, expected)) {
      throw new TypeError(`Invalid item type for ${field.cogsName}.`);
    }
    const reference = value.toReferenceObject();
    const fields = fieldMap(actual);
    for (const identity of IDENTIFICATION_FIELDS) {
      const identityField = fields.get(identity.cogsName)!;
      const child = createElement(document, identity.cogsName);
      serializeSimpleXml(identityField.typeName, value[identity.attributeName], child);
      element.appendChild(child);
    }
    const typeElement = createElement(document, "TypeOfObject");
    typeElement.appendChild(document.createTextNode(reference.$type as string));
    element.appendChild(typeElement);
    return element;
  }
  return toElementWithContext(value, element, context, field.typeName, field.allowSubtypes);
}

function targetFromElement(declared: CogsConstructor, element: Element, allowSubtypes: boolean): CogsConstructor {
  const xsiType = element.getAttributeNS(XSI_NAMESPACE, "type");
  if (xsiType === null) {
    if (declared.isAbstract) throw new TypeError(`Abstract type ${declared.cogsType} requires xsi:type.`);
    return declared;
  }
  if (!allowSubtypes) throw new TypeError(`xsi:type is not allowed for ${declared.cogsType}.`);
  const parts = xsiType.split(":");
  if (parts.length > 2 || parts[0] === "") throw new TypeError(`Invalid xsi:type ${xsiType}.`);
  const typeName = parts.at(-1)!;
  const prefix = parts.length === 2 ? parts[0]! : null;
  if (element.lookupNamespaceURI(prefix) !== TARGET_NAMESPACE) {
    throw new TypeError(`xsi:type ${xsiType} is not in the model namespace.`);
  }
  const candidate = typeForName(typeName);
  if (!isAssignable(candidate, declared) || candidate.isAbstract) throw new TypeError(`Invalid xsi:type ${typeName} for ${declared.cogsType}.`);
  return candidate;
}

function populateFromElement(target: CogsValue, element: Element, context: Context): void {
  allowedAttributes(element, [[XSI_NAMESPACE, "type"]]);
  const constructor = constructorOf(target);
  const fields = fieldsFor(constructor);
  const byName = new Map(fields.map((field, index) => [field.cogsName, { field, index }]));
  const grouped = new Map<string, Element[]>();
  let previousIndex = -1;
  for (const child of childElements(element)) {
    const name = requireTargetElement(child);
    const entry = byName.get(name);
    if (entry === undefined) throw new TypeError(`Unknown XML element ${name} for ${constructor.cogsType}.`);
    if (entry.index < previousIndex) throw new TypeError(`XML element ${name} is out of schema order.`);
    previousIndex = entry.index;
    const items = grouped.get(name) ?? [];
    items.push(child);
    grouped.set(name, items);
  }
  for (const field of fields) {
    const matches = grouped.get(field.cogsName) ?? [];
    if (!field.many && matches.length > 1) throw new TypeError(`${field.cogsName} occurs more than once.`);
    if (field.many) target[field.attributeName] = matches.map(item => deserializeFieldXml(item, field, context));
    else if (matches.length === 1) target[field.attributeName] = deserializeFieldXml(matches[0]!, field, context);
  }
}

function deserializeFieldXml(element: Element, field: FieldSpec, context: Context): unknown {
  requireTargetElement(element, field.cogsName);
  if (field.kind === "simple") return deserializeSimpleXml(field.typeName, element);
  if (field.kind === "item") return context.resolveReference(referenceFromElement(element), field.typeName, field.allowSubtypes);
  const declared = TYPE_REGISTRY.get(field.typeName);
  if (declared === undefined) throw new TypeError(`Unknown declared type ${field.typeName}.`);
  const target = targetFromElement(declared, element, field.allowSubtypes);
  const result = createInstance(target);
  populateFromElement(result, element, context);
  return result;
}

function referenceFromElement(element: Element): JsonObject {
  allowedAttributes(element);
  const grouped = new Map<string, Element>();
  const order = [...IDENTIFICATION_FIELDS.map(field => field.cogsName), "TypeOfObject"];
  let previousIndex = -1;
  for (const child of childElements(element)) {
    const name = requireTargetElement(child);
    if (grouped.has(name)) throw new TypeError(`XML reference field ${name} occurs more than once.`);
    const index = order.indexOf(name);
    if (index >= 0 && index < previousIndex) throw new TypeError(`XML reference field ${name} is out of schema order.`);
    if (index >= 0) previousIndex = index;
    grouped.set(name, child);
  }
  const allowed = new Set(["TypeOfObject", ...IDENTIFICATION_FIELDS.map(field => field.cogsName)]);
  const unknown = [...grouped.keys()].filter(name => !allowed.has(name));
  if (unknown.length > 0) throw new TypeError(`Unknown XML reference fields: ${unknown.sort().join(", ")}.`);
  const typeElement = grouped.get("TypeOfObject");
  if (typeElement === undefined) throw new TypeError("XML references require TypeOfObject.");
  allowedAttributes(typeElement);
  const typeName = elementText(typeElement).trim();
  const target = ITEM_TYPE_REGISTRY.get(typeName);
  if (target === undefined) throw new TypeError(`Unknown item type ${typeName}.`);
  const fields = fieldMap(target);
  const result: JsonObject = { $type: typeName };
  for (const identity of IDENTIFICATION_FIELDS) {
    const identityElement = grouped.get(identity.cogsName);
    if (identityElement === undefined) throw new TypeError(`XML reference is missing ${identity.cogsName}.`);
    const field = fields.get(identity.cogsName);
    if (field === undefined) throw new TypeError(`${typeName} has no ${identity.cogsName} field.`);
    result[identity.cogsName] = deserializeSimpleXml(field.typeName, identityElement);
  }
  return result;
}

function parseXml(value: string | Uint8Array): Document {
  const decoded = typeof value === "string" ? value : new TextDecoder("utf-8", { fatal: true }).decode(value);
  const text = decoded.charCodeAt(0) === 0xfeff ? decoded.slice(1) : decoded;
  if (/<!DOCTYPE/i.test(text)) throw new TypeError("XML document types are not allowed.");
  const document = new DOMParser({ onError: onWarningStopParsing }).parseFromString(text, "application/xml");
  if (document.doctype !== null) throw new TypeError("XML document types are not allowed.");
  return document;
}

function serializeDocument(document: Document, declaration: boolean): string {
  const xml = new XMLSerializer().serializeToString(document, { requireWellFormed: true });
  return declaration ? `<?xml version="1.0" encoding="utf-8"?>${xml}` : xml;
}

async function readSource(source: PathLike | Readable): Promise<string> {
  if (typeof (source as Readable).read === "function" && Symbol.asyncIterator in Object(source)) {
    const chunks: Uint8Array[] = [];
    for await (const chunk of source as Readable) {
      if (typeof chunk === "string") chunks.push(new TextEncoder().encode(chunk));
      else chunks.push(chunk instanceof Uint8Array ? chunk : new Uint8Array(chunk as ArrayBuffer));
    }
    const length = chunks.reduce((sum, item) => sum + item.byteLength, 0);
    const combined = new Uint8Array(length);
    let offset = 0;
    for (const chunk of chunks) { combined.set(chunk, offset); offset += chunk.byteLength; }
    return new TextDecoder("utf-8", { fatal: true }).decode(combined);
  }
  return await new Promise<string>((resolve, reject) => {
    readFile(source as PathLike, "utf8", (error, data) => error === null ? resolve(data) : reject(error));
  });
}

async function writeTarget(target: PathLike | Writable, value: string): Promise<void> {
  if (typeof (target as Writable).write === "function") {
    await new Promise<void>((resolve, reject) => {
      (target as Writable).write(value, "utf8", error => error === null || error === undefined ? resolve() : reject(error));
    });
    return;
  }
  await new Promise<void>((resolve, reject) => {
    writeFile(target as PathLike, value, { encoding: "utf8" }, error => error === null ? resolve() : reject(error));
  });
}

export class ItemContainer {
  readonly items: CogsItem[];
  readonly topLevelReferences: CogsItem[];

  constructor(initial: { readonly items?: CogsItem[]; readonly topLevelReferences?: CogsItem[] } = {}) {
    this.items = initial.items ?? [];
    this.topLevelReferences = initial.topLevelReferences ?? [];
  }

  toObject(): JsonObject {
    const context = new Context();
    for (const item of this.items) {
      const reference = item.toReferenceObject();
      const key = context.key(constructorOf(item).cogsType, reference);
      if (context.definedKeys.has(key)) throw new TypeError(`Duplicate full item definition: ${constructorOf(item).cogsType}.`);
      context.definedKeys.add(key);
    }
    return {
      ...(this.topLevelReferences.length === 0 ? {} : { topLevelReferences: this.topLevelReferences.map(item => item.toReferenceObject()) }),
      items: this.items.map(item => valueToObject(item, context)),
    };
  }

  static fromObject(value: unknown): ItemContainer {
    const raw = requireObjectKeys(value, ["items"], ["topLevelReferences"]);
    if (!Array.isArray(raw.items)) throw new TypeError("ItemContainer items must be an array.");
    if (raw.topLevelReferences !== undefined && !Array.isArray(raw.topLevelReferences)) {
      throw new TypeError("topLevelReferences must be an array.");
    }
    const context = new Context();
    const items = raw.items.map(item => context.loadItem(item));
    const references = (raw.topLevelReferences ?? []) as unknown[];
    return new ItemContainer({ items, topLevelReferences: references.map(item => context.resolveReference(item)) });
  }

  toJson(options: { readonly indent?: number } = {}): string { return stringifyJson(this.toObject(), options.indent); }
  static fromJson(value: string | Uint8Array): ItemContainer { return ItemContainer.fromObject(parseJson(value)); }
  static async loadJson(source: PathLike | Readable): Promise<ItemContainer> { return ItemContainer.fromJson(await readSource(source)); }
  async dumpJson(target: PathLike | Writable, options: { readonly indent?: number } = { indent: 2 }): Promise<void> {
    await writeTarget(target, this.toJson(options));
  }

  toElement(): Element {
    const document = createDocument("ItemContainer");
    const root = document.documentElement!;
    const context = new Context();
    for (const item of this.topLevelReferences) {
      const field: FieldSpec = {
        cogsName: "TopLevelReference", attributeName: "topLevelReferences", description: "",
        typeName: constructorOf(item).cogsType, kind: "item", many: true, ordered: true, allowSubtypes: true,
      };
      root.appendChild(serializeFieldXml(item, field, context, document));
    }
    for (const item of this.items) {
      const reference = item.toReferenceObject();
      const typeName = constructorOf(item).cogsType;
      const key = context.key(typeName, reference);
      if (context.definedKeys.has(key)) throw new TypeError(`Duplicate full item definition: ${typeName}.`);
      context.definedKeys.add(key);
      const element = createElement(document, typeName);
      root.appendChild(toElementWithContext(item, element, context));
    }
    return root;
  }

  static fromElement(root: Element): ItemContainer {
    requireTargetElement(root, "ItemContainer");
    allowedAttributes(root);
    const context = new Context();
    const referenceElements: Element[] = [];
    const itemElements: Element[] = [];
    let sawItems = false;
    for (const element of childElements(root)) {
      const name = requireTargetElement(element);
      if (name === "TopLevelReference") {
        if (sawItems) throw new TypeError("TopLevelReference elements must precede items.");
        referenceElements.push(element);
      } else {
        sawItems = true;
        itemElements.push(element);
      }
    }
    const items: CogsItem[] = [];
    for (const element of itemElements) {
      const typeName = requireTargetElement(element);
      const target = ITEM_TYPE_REGISTRY.get(typeName);
      if (target === undefined) throw new TypeError(`Unknown item element ${typeName}.`);
      if (target.isAbstract) throw new TypeError(`Abstract item type cannot be instantiated: ${typeName}.`);
      allowedAttributes(element);
      const elements = childElements(element);
      const byName = new Map(elements.map(item => [requireTargetElement(item), item]));
      const reference: JsonObject = { $type: typeName };
      const fields = fieldMap(target);
      for (const identity of IDENTIFICATION_FIELDS) {
        const identityElement = byName.get(identity.cogsName);
        if (identityElement === undefined) throw new TypeError(`Item ${typeName} requires ${identity.cogsName}.`);
        const field = fields.get(identity.cogsName)!;
        reference[identity.cogsName] = deserializeSimpleXml(field.typeName, identityElement);
      }
      const item = context.resolveReference(reference, typeName);
      const key = context.key(typeName, reference);
      if (context.definedKeys.has(key)) throw new TypeError(`Duplicate full item definition: ${typeName}.`);
      context.definedKeys.add(key);
      populateFromElement(item, element, context);
      items.push(item);
    }
    const topLevelReferences = referenceElements.map(element => context.resolveReference(referenceFromElement(element)));
    return new ItemContainer({ items, topLevelReferences });
  }

  toXml(options: { readonly xmlDeclaration?: boolean } = {}): string {
    return serializeDocument(this.toElement().ownerDocument!, options.xmlDeclaration ?? false);
  }
  static fromXml(value: string | Uint8Array): ItemContainer { return ItemContainer.fromElement(parseXml(value).documentElement!); }
  static async loadXml(source: PathLike | Readable): Promise<ItemContainer> { return ItemContainer.fromXml(await readSource(source)); }
  async dumpXml(target: PathLike | Writable, options: { readonly xmlDeclaration?: boolean } = { xmlDeclaration: true }): Promise<void> {
    await writeTarget(target, this.toXml(options));
  }
}
