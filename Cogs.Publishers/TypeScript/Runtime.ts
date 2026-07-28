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

const TARGET_NAMESPACE: string = __TARGET_NAMESPACE__;
const NAMESPACE_PREFIX: string = __NAMESPACE_PREFIX__;
const XSI_PREFIX = NAMESPACE_PREFIX === "xsi" ? "cogs_xsi" : "xsi";
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

type JsonObject = { [key: string]: unknown };

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
    if (current instanceof CogsDuration) return JSON.stringify(current.value);
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
  if (!/^-?(?:0|[1-9]\d*)(?:\.\d+)?$/.test(value)) {
    throw new TypeError(`Invalid COGS decimal lexical value: ${JSON.stringify(value)}.`);
  }
  return value;
}

/** An exact, string-backed decimal value. */
export class CogsDecimal {
  readonly value: string;

  constructor(value: string | bigint | CogsDecimal) {
    if (value instanceof CogsDecimal) this.value = value.value;
    else this.value = normalizeDecimal(String(value));
  }

  toString(): string {
    return this.value;
  }
}

/** A lossless full XSD duration lexical value, used in both JSON and XML. */
export class CogsDuration {
  readonly value: string;

  constructor(value: string | CogsDuration) {
    const lexical = value instanceof CogsDuration ? value.value : value;
    if (!/^-?P(?=\d|T(?:\d|\.\d))(?:\d+Y)?(?:\d+M)?(?:\d+D)?(?:T(?=\d|\.\d)(?:\d+H)?(?:\d+M)?(?:(?:\d+(?:\.\d*)?|\.\d+)S)?)?$/.test(lexical)) {
      throw new TypeError(`Invalid XSD duration: ${JSON.stringify(lexical)}.`);
    }
    this.value = lexical;
  }

  static fromXml(value: string): CogsDuration {
    return new CogsDuration(value);
  }

  toXml(): string {
    return this.value;
  }

  toString(): string { return this.value; }
}

function validateTimezone(value: string | undefined): void {
  if (value === undefined || value === "Z") return;
  const match = /^[+-](\d{2}):(\d{2})$/.exec(value);
  if (match === null || Number(match[1]) > 14 || Number(match[2]) > 59
      || (Number(match[1]) === 14 && Number(match[2]) !== 0)) {
    throw new TypeError(`Invalid timezone: ${JSON.stringify(value)}.`);
  }
}

function validateCalendarYear(year: number): number {
  if (!Number.isInteger(year) || year < -2_147_483_648 || year > 2_147_483_647) {
    throw new RangeError("COGS calendar years must fit in a signed 32-bit integer.");
  }
  if (year === 0) throw new TypeError("XSD dates do not have a year zero.");
  return year;
}

function calendarYearFromLexical(value: string): number {
  const year = BigInt(value);
  if (year < -2_147_483_648n || year > 2_147_483_647n) {
    throw new RangeError("COGS calendar years must fit in a signed 32-bit integer.");
  }
  return validateCalendarYear(Number(year));
}

function calendarYearFromObject(value: unknown): number {
  const result = parseInteger(value, "int");
  if (typeof result !== "number") throw new TypeError("Year must be an integer.");
  return validateCalendarYear(result);
}

function isLeapYear(year: number): boolean {
  const astronomicalYear = year < 0 ? year + 1 : year;
  return astronomicalYear % 400 === 0 || (astronomicalYear % 4 === 0 && astronomicalYear % 100 !== 0);
}

function validateDateParts(year: number, month: number, day: number): void {
  validateCalendarYear(year);
  if (!Number.isInteger(month) || month < 1 || month > 12) throw new TypeError("Month must be between 1 and 12.");
  const days = [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (!Number.isInteger(day) || day < 1 || day > days[month - 1]!) throw new TypeError("Day is invalid for the month.");
}

function validateTimeParts(hour: number, minute: number, second: number): void {
  if (!Number.isInteger(hour) || hour < 0 || hour > 24
      || !Number.isInteger(minute) || minute < 0 || minute > 59
      || !Number.isInteger(second) || second < 0 || second > 59
      || (hour === 24 && (minute !== 0 || second !== 0))) {
    throw new TypeError("Invalid time value.");
  }
}

function validateEndOfDayFraction(hour: number, fraction: string | undefined): void {
  if (hour === 24 && fraction !== undefined && /[1-9]/.test(fraction)) {
    throw new TypeError("24:00:00 cannot have a nonzero fractional component.");
  }
}

/** An ISO dateTime lexical value. */
export class CogsDateTime {
  constructor(readonly value: string) {
    const match = /^(-?(?:\d{4}|[1-9]\d{4,}))-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(\.\d+)?(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid dateTime: ${JSON.stringify(value)}.`);
    validateDateParts(calendarYearFromLexical(match[1]!), Number(match[2]), Number(match[3]));
    validateTimeParts(Number(match[4]), Number(match[5]), Number(match[6]));
    validateEndOfDayFraction(Number(match[4]), match[7]);
    validateTimezone(match[8]);
  }

  toString(): string { return this.value; }
}

/** An ISO date lexical value. */
export class CogsDateOnly {
  constructor(readonly value: string) {
    const match = /^(-?(?:\d{4}|[1-9]\d{4,}))-(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid date: ${JSON.stringify(value)}.`);
    validateDateParts(calendarYearFromLexical(match[1]!), Number(match[2]), Number(match[3]));
    validateTimezone(match[4]);
  }

  toString(): string { return this.value; }
}

/** An ISO time lexical value. */
export class CogsTime {
  constructor(readonly value: string) {
    const match = /^(\d{2}):(\d{2}):(\d{2})(\.\d+)?(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid time: ${JSON.stringify(value)}.`);
    validateTimeParts(Number(match[1]), Number(match[2]), Number(match[3]));
    validateEndOfDayFraction(Number(match[1]), match[4]);
    validateTimezone(match[5]);
  }

  toString(): string { return this.value; }
}

function yearText(year: number): string {
  validateCalendarYear(year);
  const negative = year < 0;
  const digits = Math.abs(year).toString().padStart(4, "0");
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
  readonly year: number;
  readonly month: number;
  readonly timezone: string | undefined;

  constructor(year: number, month: number, timezone?: string, private readonly lexical: string | undefined = undefined) {
    this.year = validateCalendarYear(year);
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
    return new GYearMonth(calendarYearFromObject(raw.Year), parseSmallInteger(raw.Month, "Month"), optionalString(raw.Timezone));
  }

  toXml(): string { return this.lexical ?? `${yearText(this.year)}-${String(this.month).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GYearMonth {
    const match = /^(-?(?:\d{4}|[1-9]\d{4,}))-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gYearMonth: ${JSON.stringify(value)}.`);
    return new GYearMonth(calendarYearFromLexical(match[1]!), Number(match[2]), match[3], value);
  }
}

export class GYear {
  readonly year: number;
  readonly timezone: string | undefined;

  constructor(year: number, timezone?: string, private readonly lexical: string | undefined = undefined) {
    this.year = validateCalendarYear(year);
    this.timezone = timezone;
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Year: this.year, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GYear {
    const raw = requireObjectKeys(value, ["Year"], ["Timezone"]);
    return new GYear(calendarYearFromObject(raw.Year), optionalString(raw.Timezone));
  }

  toXml(): string { return this.lexical ?? `${yearText(this.year)}${this.timezone ?? ""}`; }

  static fromXml(value: string): GYear {
    const match = /^(-?(?:\d{4}|[1-9]\d{4,}))(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gYear: ${JSON.stringify(value)}.`);
    return new GYear(calendarYearFromLexical(match[1]!), match[2], value);
  }
}

export class GMonthDay {
  constructor(readonly month: number, readonly day: number, readonly timezone?: string, private readonly lexical: string | undefined = undefined) {
    validateDateParts(2000, month, day);
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Month: this.month, Day: this.day, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GMonthDay {
    const raw = requireObjectKeys(value, ["Month", "Day"], ["Timezone"]);
    return new GMonthDay(parseSmallInteger(raw.Month, "Month"), parseSmallInteger(raw.Day, "Day"), optionalString(raw.Timezone));
  }

  toXml(): string { return this.lexical ?? `--${String(this.month).padStart(2, "0")}-${String(this.day).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GMonthDay {
    const match = /^--(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gMonthDay: ${JSON.stringify(value)}.`);
    return new GMonthDay(Number(match[1]), Number(match[2]), match[3], value);
  }
}

export class GMonth {
  constructor(readonly month: number, readonly timezone?: string, private readonly lexical: string | undefined = undefined) {
    if (!Number.isInteger(month) || month < 1 || month > 12) throw new TypeError("Month must be between 1 and 12.");
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Month: this.month, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GMonth {
    const raw = requireObjectKeys(value, ["Month"], ["Timezone"]);
    return new GMonth(parseSmallInteger(raw.Month, "Month"), optionalString(raw.Timezone));
  }

  toXml(): string { return this.lexical ?? `--${String(this.month).padStart(2, "0")}--${this.timezone ?? ""}`; }

  static fromXml(value: string): GMonth {
    const match = /^--(\d{2})--(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gMonth: ${JSON.stringify(value)}.`);
    return new GMonth(Number(match[1]), match[2], value);
  }
}

export class GDay {
  constructor(readonly day: number, readonly timezone?: string, private readonly lexical: string | undefined = undefined) {
    if (!Number.isInteger(day) || day < 1 || day > 31) throw new TypeError("Day must be between 1 and 31.");
    validateTimezone(timezone);
  }

  toObject(): JsonObject { return { Day: this.day, ...(this.timezone === undefined ? {} : { Timezone: this.timezone }) }; }

  static fromObject(value: unknown): GDay {
    const raw = requireObjectKeys(value, ["Day"], ["Timezone"]);
    return new GDay(parseSmallInteger(raw.Day, "Day"), optionalString(raw.Timezone));
  }

  toXml(): string { return this.lexical ?? `---${String(this.day).padStart(2, "0")}${this.timezone ?? ""}`; }

  static fromXml(value: string): GDay {
    const match = /^---(\d{2})(Z|[+-]\d{2}:\d{2})?$/.exec(value);
    if (match === null) throw new TypeError(`Invalid gDay: ${JSON.stringify(value)}.`);
    return new GDay(Number(match[1]), match[2], value);
  }
}

export class LangString {
  constructor(readonly language: string, readonly value: string) {
    if (typeof language !== "string" || typeof value !== "string") throw new TypeError("LangString values must be strings.");
    validateLanguage(language);
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
    return { [this.kind]: this.value.toXml() };
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
    if (/^-?(?:\d{4}|[1-9]\d{4,})-\d{2}-\d{2}/.test(value)) return CogsDate.date(value);
    if (/^-?(?:\d{4}|[1-9]\d{4,})-\d{2}/.test(value)) return CogsDate.gYearMonth(GYearMonth.fromXml(value));
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

function validateLanguage(value: string): string {
  const languageTag = /^(?:(?:[A-Z]{2,3}(?:-[A-Z]{3}){0,3}|[A-Z]{4}|[A-Z]{5,8})(?:-[A-Z]{4})?(?:-(?:[A-Z]{2}|\d{3}))?(?:-(?:[A-Z0-9]{5,8}|\d[A-Z0-9]{3}))*(?:-[0-9A-WY-Z](?:-[A-Z0-9]{2,8})+)*(?:-X(?:-[A-Z0-9]{1,8})+)?|X(?:-[A-Z0-9]{1,8})+|(?:EN-GB-OED|I-(?:AMI|Bnn|DEFAULT|ENochian|HAK|KLINGON|LUX|MINGO|NAVAJO|PWN|TAO|TAY|TSU)|SGN-(?:BE-FR|BE-NL|CH-DE)|ART-LOJBAN|CEL-GAULISH|NO-(?:BOK|NYN)|ZH-(?:GUOYU|HAKKA|MIN|MIN-NAN|XIANG)))$/i;
  if (!languageTag.test(value)) throw new TypeError(`Invalid BCP 47 language tag: ${JSON.stringify(value)}.`);
  return value;
}

function validateUriReference(value: string): string {
  if (!/^(?:[A-Za-z0-9._~:/?#\[\]@!$&'()*+,;=-]|%[0-9A-Fa-f]{2})*$/.test(value)) {
    throw new TypeError(`Invalid RFC 3986 URI reference: ${JSON.stringify(value)}.`);
  }
  const fragment = value.indexOf("#");
  if (fragment >= 0 && value.indexOf("#", fragment + 1) >= 0) {
    throw new TypeError(`Invalid RFC 3986 URI reference: ${JSON.stringify(value)}.`);
  }
  const query = value.indexOf("?");
  const hierarchicalEnd = [value.indexOf("/"), query, fragment].filter(index => index >= 0).sort((a, b) => a - b)[0] ?? value.length;
  const firstColon = value.indexOf(":");
  if (firstColon >= 0 && firstColon < hierarchicalEnd && !/^[A-Za-z][A-Za-z0-9+.-]*$/.test(value.slice(0, firstColon))) {
    throw new TypeError(`Invalid RFC 3986 URI scheme: ${JSON.stringify(value)}.`);
  }
  if ((value.match(/\[/g)?.length ?? 0) !== (value.match(/\]/g)?.length ?? 0)) {
    throw new TypeError(`Invalid RFC 3986 URI reference: ${JSON.stringify(value)}.`);
  }
  return value;
}

function validateStringType(typeName: string, value: unknown): string {
  const result = requireString(value, typeName);
  const lower = typeName.toLowerCase();
  if (lower === "language") return validateLanguage(result);
  if (lower === "anyuri") return validateUriReference(result);
  return result;
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

function parseXmlInteger(value: string, typeName: string): number | bigint {
  if (!/^[+-]?\d+$/.test(value)) throw new TypeError(`${typeName} is not an XSD integer lexical value.`);
  return parseInteger(new JsonNumber(BigInt(value).toString()), typeName);
}

function parseDuration(value: unknown): CogsDuration {
  if (value instanceof CogsDuration) return value;
  return new CogsDuration(requireString(value, "duration"));
}

function serializeSimpleObject(typeName: string, value: unknown): unknown {
  const lower = typeName.toLowerCase();
  if (lower === "string" || lower === "language" || lower === "anyuri") return validateStringType(typeName, value);
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
    return value.value;
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
  if (lower === "string" || lower === "language" || lower === "anyuri") return validateStringType(typeName, value);
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

interface IdentityNode {
  readonly children: Map<string, IdentityNode>;
  item?: CogsItem;
  defined: boolean;
}

const DEFINITION_STATE = Symbol("cogs-definition-state");

class Context {
  private readonly identityRoots = new Map<string, IdentityNode>();

  private identityNode(typeName: string, raw: JsonObject): IdentityNode {
    let node: IdentityNode;
    const root = this.identityRoots.get(typeName);
    if (root === undefined) {
      node = { children: new Map<string, IdentityNode>(), defined: false };
      this.identityRoots.set(typeName, node);
    } else {
      node = root;
    }
    for (const field of IDENTIFICATION_FIELDS) {
      if (!own(raw, field.cogsName)) throw new TypeError(`Reference is missing identification field ${field.cogsName}.`);
      const value = raw[field.cogsName];
      if (typeof value !== "string" || value.length === 0) {
        throw new TypeError(`Identification field ${field.cogsName} must be a nonempty string.`);
      }
      let child: IdentityNode | undefined = node.children.get(value);
      if (child === undefined) {
        child = { children: new Map<string, IdentityNode>(), defined: false };
        node.children.set(value, child);
      }
      node = child;
    }
    return node;
  }

  registerDefinition(typeName: string, raw: JsonObject, item: CogsItem): void {
    const node = this.identityNode(typeName, raw);
    if (node.defined) throw new TypeError(`Duplicate full item definition: ${typeName}.`);
    if (node.item !== undefined && node.item !== item) {
      throw new TypeError(`Two objects use the same complete identity tuple for ${typeName}.`);
    }
    node.item = item;
    node.defined = true;
    item[DEFINITION_STATE] = true;
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
      if (!isAssignable(actual, expected) || (!allowSubtypes && actual !== expected)) {
        if (allowSubtypes) throw new TypeError(`${raw.$type} is not assignable to ${expectedType}.`);
        throw new TypeError(`Item reference ${expectedType} requires the exact type; found ${raw.$type}.`);
      }
    }
    const fields = fieldMap(actual);
    const identityValues = new Map<string, unknown>();
    for (const identity of IDENTIFICATION_FIELDS) {
      const field = fields.get(identity.cogsName);
      if (field === undefined) throw new TypeError(`Item ${raw.$type} has no ${identity.cogsName} field.`);
      const identityValue = deserializeSimpleObject(field.typeName, raw[identity.cogsName]);
      if (typeof identityValue !== "string" || identityValue.length === 0) {
        throw new TypeError(`Identification field ${identity.cogsName} must be nonempty.`);
      }
      identityValues.set(identity.attributeName, identityValue);
    }
    const node = this.identityNode(raw.$type, raw);
    let result = node.item;
    if (result === undefined) {
      result = createInstance(actual);
      result[DEFINITION_STATE] = false;
      for (const identity of IDENTIFICATION_FIELDS) {
        result[identity.attributeName] = identityValues.get(identity.attributeName);
      }
      node.item = result;
    }
    return result;
  }

  prepareItem(value: unknown): readonly [CogsItem, JsonObject] {
    if (!isObject(value) || typeof value.$type !== "string") throw new TypeError("Serialized items require a string $type discriminator.");
    const reference: JsonObject = { $type: value.$type };
    for (const field of IDENTIFICATION_FIELDS) if (own(value, field.cogsName)) reference[field.cogsName] = value[field.cogsName];
    const result = this.resolveReference(reference, value.$type);
    this.registerDefinition(value.$type, reference, result);
    return [result, value];
  }

  loadItem(value: unknown): CogsItem {
    const [result, definition] = this.prepareItem(value);
    populateFromObject(result, definition, this);
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
    if (expected === undefined || !isAssignable(actual, expected) || (!field.allowSubtypes && actual !== expected)) {
      throw new TypeError(`Invalid item type for ${field.cogsName}.`);
    }
    return value.toReferenceObject();
  }
  const expected = TYPE_REGISTRY.get(field.typeName);
  if (expected === undefined || !isAssignable(actual, expected) || (!field.allowSubtypes && actual !== expected)) {
    throw new TypeError(`Invalid object type for ${field.cogsName}.`);
  }
  return valueToObject(value, context, field.allowSubtypes);
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
    if (!field.allowSubtypes) throw new TypeError(`$type is not allowed for ${field.cogsName}.`);
    if (typeof value.$type !== "string") throw new TypeError("$type must be a string.");
    const candidate = typeForName(value.$type);
    if (!isAssignable(candidate, target) || candidate.isAbstract) {
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

function valueToObject(value: CogsValue, context: Context, includeCompositeType = false): JsonObject {
  const constructor = constructorOf(value);
  if (constructor.isAbstract) throw new TypeError(`Abstract type cannot be serialized: ${constructor.cogsType}.`);
  const result: JsonObject = Object.create(null) as JsonObject;
  if (constructor.isItem || includeCompositeType) result.$type = constructor.cogsType;
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
  [DEFINITION_STATE] = true;

  /** False only for an unresolved external-reference placeholder. */
  get isDefined(): boolean { return this[DEFINITION_STATE]; }

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
  document.documentElement!.setAttributeNS(XMLNS_NAMESPACE, `xmlns:${XSI_PREFIX}`, XSI_NAMESPACE);
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
  } else text = validateStringType(typeName, value);
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
  const rawText = elementText(element);
  const text = rawText.trim();
  if (lower === "string") return rawText;
  if (lower === "language" || lower === "anyuri") return validateStringType(typeName, rawText);
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
    return parseXmlInteger(text, typeName);
  }
  if (lower === "float" || lower === "double") {
    const result = Number(text);
    if (!Number.isFinite(result)) throw new TypeError(`${typeName} must be finite.`);
    return result;
  }
  return validateStringType(typeName, rawText);
}

function toElementWithContext(value: CogsValue, element: Element, context: Context, declaredType?: string, allowSubtypes = false): Element {
  const actual = constructorOf(value);
  if (actual.isAbstract) throw new TypeError(`Abstract type cannot be serialized: ${actual.cogsType}.`);
  if (declaredType !== undefined) {
    const declared = typeForName(declaredType);
    if (!isAssignable(actual, declared) || (!allowSubtypes && actual !== declared)) {
      throw new TypeError(`${actual.cogsType} is not allowed where ${declaredType} is declared.`);
    }
    if (allowSubtypes) {
      element.setAttributeNS(XSI_NAMESPACE, `${XSI_PREFIX}:type`, `${NAMESPACE_PREFIX}:${actual.cogsType}`);
    }
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
    if (expected === undefined || !isAssignable(actual, expected) || (!field.allowSubtypes && actual !== expected)) {
      throw new TypeError(`Invalid item type for ${field.cogsName}.`);
    }
    element.setAttribute("isReference", "true");
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

function targetFromElement(declared: CogsConstructor, element: Element, allowSubtypes: boolean, requireType = false): CogsConstructor {
  const xsiType = element.getAttributeNS(XSI_NAMESPACE, "type");
  if (xsiType === null) {
    if (requireType) throw new TypeError(`Composite type ${declared.cogsType} requires a qualified xsi:type.`);
    if (declared.isAbstract) throw new TypeError(`Abstract type ${declared.cogsType} requires xsi:type.`);
    return declared;
  }
  if (!allowSubtypes) throw new TypeError(`xsi:type is not allowed for ${declared.cogsType}.`);
  const parts = xsiType.split(":");
  if (parts.length !== 2 || parts[0] === "" || parts[1] === "") throw new TypeError(`xsi:type must be a qualified QName: ${xsiType}.`);
  const typeName = parts[1]!;
  const prefix = parts[0]!;
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
  const target = targetFromElement(declared, element, field.allowSubtypes, field.allowSubtypes);
  const result = createInstance(target);
  populateFromElement(result, element, context);
  return result;
}

function referenceFromElement(element: Element): JsonObject {
  allowedAttributes(element, [[null, "isReference"]]);
  const marker = element.getAttributeNS(null, "isReference");
  if (marker !== null && marker !== "true" && marker !== "1") {
    throw new TypeError("The unqualified isReference attribute must have the fixed boolean value true (lexically 'true' or '1').");
  }
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
      context.registerDefinition(constructorOf(item).cogsType, reference, item);
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
    const prepared = raw.items.map(item => context.prepareItem(item));
    for (const [item, definition] of prepared) populateFromObject(item, definition, context);
    const items = prepared.map(([item]) => item);
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
      context.registerDefinition(typeName, reference, item);
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
    const preparedItems: (readonly [CogsItem, Element])[] = [];
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
      context.registerDefinition(typeName, reference, item);
      preparedItems.push([item, element]);
    }
    for (const [item, element] of preparedItems) populateFromElement(item, element, context);
    const items = preparedItems.map(([item]) => item);
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
