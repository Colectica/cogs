from __future__ import annotations

import io
import json
import math
import os
import re
from dataclasses import dataclass, field, fields
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, ClassVar, IO, Mapping
from xml.etree import ElementTree as ET

TARGET_NAMESPACE = __TARGET_NAMESPACE__
NAMESPACE_PREFIX = __NAMESPACE_PREFIX__
IDENTIFICATION_FIELDS = __IDENTIFICATION_FIELDS__
XML_NAMESPACE = "http://www.w3.org/XML/1998/namespace"
XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance"
XSI_PREFIX = "cogs_xsi" if NAMESPACE_PREFIX == "xsi" else "xsi"

ET.register_namespace("", TARGET_NAMESPACE)
ET.register_namespace(XSI_PREFIX, XSI_NAMESPACE)


def _q(name: str) -> str:
    return f"{{{TARGET_NAMESPACE}}}{name}"


def _local_name(name: str) -> str:
    if not isinstance(name, str):
        raise ValueError("XML comments and processing instructions are not allowed.")
    return name.rsplit("}", 1)[-1]


def _model_local_name(element: ET.Element) -> str:
    if not isinstance(element.tag, str) or not element.tag.startswith("{" + TARGET_NAMESPACE + "}"):
        raise ValueError(f"Expected an element in namespace {TARGET_NAMESPACE!r}.")
    return _local_name(element.tag)


def _check_no_mixed_content(element: ET.Element, description: str) -> None:
    if element.text and element.text.strip():
        raise ValueError(f"{description} cannot contain mixed text content.")
    for child in element:
        if child.tail and child.tail.strip():
            raise ValueError(f"{description} cannot contain mixed text content.")


def _json_dump_value(value: Any, indent: int | None = None, level: int = 0) -> str:
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, CogsDecimal):
        return value.lexical
    if isinstance(value, int):
        return str(value)
    if isinstance(value, Decimal):
        return CogsDecimal(value).lexical
    if isinstance(value, float):
        if not math.isfinite(value):
            raise ValueError("JSON numbers must be finite.")
        return json.dumps(value, allow_nan=False)
    if isinstance(value, list):
        if not value:
            return "[]"
        if indent is None:
            return "[" + ",".join(_json_dump_value(item) for item in value) + "]"
        child = level + 1
        padding = " " * (indent * child)
        closing = " " * (indent * level)
        return "[\n" + padding + (",\n" + padding).join(
            _json_dump_value(item, indent, child) for item in value
        ) + "\n" + closing + "]"
    if isinstance(value, dict):
        if not value:
            return "{}"
        pairs: list[str] = []
        for key, item in value.items():
            if not isinstance(key, str):
                raise TypeError("JSON object keys must be strings.")
            separator = ": " if indent is not None else ":"
            pairs.append(
                json.dumps(key, ensure_ascii=False)
                + separator
                + _json_dump_value(item, indent, level + 1)
            )
        if indent is None:
            return "{" + ",".join(pairs) + "}"
        child = level + 1
        padding = " " * (indent * child)
        closing = " " * (indent * level)
        return "{\n" + padding + (",\n" + padding).join(pairs) + "\n" + closing + "}"
    raise TypeError(f"Object of type {type(value).__name__} is not JSON serializable.")


def _json_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON field: {key}")
        result[key] = value
    return result


def _reject_json_constant(value: str) -> Any:
    raise ValueError(f"Invalid JSON number: {value}")


def _json_load_value(value: str | bytes | bytearray) -> Any:
    return json.loads(
        value,
        parse_int=int,
        parse_float=Decimal,
        parse_constant=_reject_json_constant,
        object_pairs_hook=_json_object,
    )


def _parse_xml(
    value: str | bytes | bytearray,
) -> tuple[ET.Element, dict[str, str], dict[int, dict[str, str]]]:
    data = value.encode("utf-8") if isinstance(value, str) else bytes(value)
    if re.search(br"<!\s*(?:DOCTYPE|ENTITY)\b", data, re.IGNORECASE):
        raise ValueError("DTD and entity declarations are forbidden in COGS XML.")
    pending_namespaces: list[tuple[str, str]] = []
    namespace_stack: list[dict[str, str]] = []
    element_namespaces: dict[int, dict[str, str]] = {}
    parser = ET.iterparse(io.BytesIO(data), events=("start-ns", "start", "end"))
    for event, payload in parser:
        if event == "start-ns":
            prefix, uri = payload
            pending_namespaces.append((prefix or "", uri))
        elif event == "start":
            namespaces = dict(namespace_stack[-1]) if namespace_stack else {}
            namespaces.update(pending_namespaces)
            pending_namespaces.clear()
            namespace_stack.append(namespaces)
            element_namespaces[id(payload)] = namespaces
        else:
            namespace_stack.pop()
    root = parser.root
    return root, element_namespaces.get(id(root), {}), element_namespaces


def _namespace_map(namespaces: Mapping[str, str] | None) -> dict[str, str]:
    result = {
        "": TARGET_NAMESPACE,
        NAMESPACE_PREFIX: TARGET_NAMESPACE,
        XSI_PREFIX: XSI_NAMESPACE,
    }
    if namespaces is not None:
        result.update(namespaces)
    return result


_DECIMAL_PATTERN = re.compile(r"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$")
_LANGUAGE_REGULAR_PATTERN = re.compile(
    r"^(?:(?:[A-Za-z]{2,3}(?:-[A-Za-z]{3}){0,3}|[A-Za-z]{4}|[A-Za-z]{5,8})"
    r"(?:-[A-Za-z]{4})?(?:-(?:[A-Za-z]{2}|[0-9]{3}))?"
    r"(?:-(?:[A-Za-z0-9]{5,8}|[0-9][A-Za-z0-9]{3}))*"
    r"(?:-[0-9A-WY-Za-wy-z](?:-[A-Za-z0-9]{2,8})+)*"
    r"(?:-[xX](?:-[A-Za-z0-9]{1,8})+)?|[xX](?:-[A-Za-z0-9]{1,8})+)$"
)
_GRANDFATHERED_LANGUAGE_TAGS = frozenset(
    {
        "en-gb-oed", "i-ami", "i-bnn", "i-default", "i-enochian", "i-hak",
        "i-klingon", "i-lux", "i-mingo", "i-navajo", "i-pwn", "i-tao",
        "i-tay", "i-tsu", "sgn-be-fr", "sgn-be-nl", "sgn-ch-de", "art-lojban",
        "cel-gaulish", "no-bok", "no-nyn", "zh-guoyu", "zh-hakka", "zh-min",
        "zh-min-nan", "zh-xiang",
    }
)
_URI_REFERENCE_CHARACTER_PATTERN = re.compile(
    r"^(?:[A-Za-z0-9._~:/?#\[\]@!$&'()*+,;=-]|%[0-9A-Fa-f]{2})*$"
)
_TIMEZONE_PATTERN = re.compile(r"^(?:Z|(?P<sign>[+-])(?P<hour>[0-9]{2}):(?P<minute>[0-9]{2}))$")
_YEAR_PATTERN = r"-?(?:[0-9]{4}|[1-9][0-9]{4,})"
_DATE_PATTERN = re.compile(
    rf"^(?P<year>{_YEAR_PATTERN})-(?P<month>[0-9]{{2}})-(?P<day>[0-9]{{2}})(?P<tz>Z|[+-][0-9]{{2}}:[0-9]{{2}})?$"
)
_TIME_PATTERN = re.compile(
    r"^(?P<hour>[0-9]{2}):(?P<minute>[0-9]{2}):(?P<second>[0-9]{2})(?P<fraction>\.[0-9]+)?(?P<tz>Z|[+-][0-9]{2}:[0-9]{2})?$"
)
_DATETIME_PATTERN = re.compile(
    rf"^(?P<year>{_YEAR_PATTERN})-(?P<month>[0-9]{{2}})-(?P<day>[0-9]{{2}})T"
    r"(?P<hour>[0-9]{2}):(?P<minute>[0-9]{2}):(?P<second>[0-9]{2})(?P<fraction>\.[0-9]+)?(?P<tz>Z|[+-][0-9]{2}:[0-9]{2})?$"
)
_DURATION_PATTERN = re.compile(
    r"^-?P(?=[0-9]|T(?:[0-9]|\.[0-9]))(?:[0-9]+Y)?(?:[0-9]+M)?(?:[0-9]+D)?"
    r"(?:T(?=[0-9]|\.[0-9])(?:[0-9]+H)?(?:[0-9]+M)?"
    r"(?:(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)S)?)?$"
)
_XML_DECIMAL_PATTERN = re.compile(r"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)$")


def _validate_timezone(value: str | None) -> None:
    if value is None:
        return
    match = _TIMEZONE_PATTERN.fullmatch(value)
    if match is None:
        raise ValueError(f"Invalid XML Schema timezone: {value!r}")
    if value == "Z":
        return
    hour = int(match.group("hour"))
    minute = int(match.group("minute"))
    if hour > 14 or minute > 59 or (hour == 14 and minute != 0):
        raise ValueError(f"XML Schema timezone is outside +/-14:00: {value!r}")


def _validate_year(value: str) -> int:
    year = int(value)
    if year == 0:
        raise ValueError("XML Schema year zero is not valid.")
    if year < -(2**31) or year > 2**31 - 1:
        raise ValueError("COGS calendar years must fit in a signed 32-bit integer.")
    return year


def _days_in_month(year: int, month: int) -> int:
    if month == 2:
        astronomical = year + 1 if year < 0 else year
        leap = astronomical % 4 == 0 and (astronomical % 100 != 0 or astronomical % 400 == 0)
        return 29 if leap else 28
    return 30 if month in {4, 6, 9, 11} else 31


def _validate_date_parts(year_text: str, month_text: str, day_text: str) -> None:
    year = _validate_year(year_text)
    month = int(month_text)
    day = int(day_text)
    if month < 1 or month > 12 or day < 1 or day > _days_in_month(year, month):
        raise ValueError("Date components are outside the XML Schema calendar.")


def _validate_time_parts(match: re.Match[str]) -> None:
    hour = int(match.group("hour"))
    minute = int(match.group("minute"))
    second = int(match.group("second"))
    if hour > 24 or minute > 59 or second > 59:
        raise ValueError("Time components are outside the XML Schema clock.")
    fraction = match.group("fraction")
    if hour == 24 and (
        minute != 0
        or second != 0
        or fraction is not None and any(character != "0" for character in fraction[1:])
    ):
        raise ValueError("24:00:00 cannot have nonzero or fractional components.")
    _validate_timezone(match.group("tz"))


def _is_valid_language(value: str) -> bool:
    return (
        _LANGUAGE_REGULAR_PATTERN.fullmatch(value) is not None
        or value.lower() in _GRANDFATHERED_LANGUAGE_TAGS
    )


def _is_valid_uri_reference(value: str) -> bool:
    if _URI_REFERENCE_CHARACTER_PATTERN.fullmatch(value) is None or value.count("#") > 1:
        return False
    delimiter_positions = [
        position for position in (value.find("/"), value.find("?"), value.find("#"))
        if position >= 0
    ]
    first_delimiter = min(delimiter_positions, default=len(value))
    colon = value.find(":")
    if colon >= 0 and colon < first_delimiter:
        if re.fullmatch(r"[A-Za-z][A-Za-z0-9+.-]*", value[:colon]) is None:
            return False
    return value.count("[") == value.count("]")


def _decimal_from_xml(raw: str) -> CogsDecimal:
    if _XML_DECIMAL_PATTERN.fullmatch(raw) is None:
        raise ValueError(f"Invalid decimal: {raw!r}")
    negative = raw.startswith("-")
    unsigned = raw[1:] if raw[:1] in {"+", "-"} else raw
    integer, separator, fraction = unsigned.partition(".")
    integer = (integer.lstrip("0") or "0") if integer else "0"
    lexical = ("-" if negative else "") + integer
    if separator and fraction:
        lexical += "." + fraction
    return CogsDecimal(lexical)


@dataclass(frozen=True)
class CogsDecimal:
    lexical: str

    def __init__(self, value: CogsDecimal | Decimal | int | str) -> None:
        if isinstance(value, CogsDecimal):
            lexical = value.lexical
        elif isinstance(value, bool) or isinstance(value, float):
            raise TypeError("CogsDecimal requires a decimal lexical value, Decimal, or integer.")
        else:
            lexical = str(value)
        if _DECIMAL_PATTERN.fullmatch(lexical) is None:
            raise ValueError("decimal must use a JSON-compatible XSD decimal lexical form without exponent.")
        try:
            parsed = Decimal(lexical)
        except InvalidOperation as exc:
            raise ValueError(f"Invalid decimal: {lexical!r}") from exc
        if not parsed.is_finite():
            raise ValueError("decimal must be finite.")
        object.__setattr__(self, "lexical", lexical)

    def __str__(self) -> str:
        return self.lexical

    def to_decimal(self) -> Decimal:
        return Decimal(self.lexical)


@dataclass(frozen=True)
class CogsDateTime:
    lexical: str

    def __post_init__(self) -> None:
        if not isinstance(self.lexical, str):
            raise TypeError("dateTime must be a string.")
        match = _DATETIME_PATTERN.fullmatch(self.lexical)
        if match is None:
            raise ValueError(f"Invalid dateTime: {self.lexical!r}")
        _validate_date_parts(match.group("year"), match.group("month"), match.group("day"))
        _validate_time_parts(match)

    def to_json_value(self) -> str:
        return self.lexical

    def to_xml_text(self) -> str:
        return self.lexical


@dataclass(frozen=True)
class CogsDateOnly:
    lexical: str

    def __post_init__(self) -> None:
        if not isinstance(self.lexical, str):
            raise TypeError("date must be a string.")
        match = _DATE_PATTERN.fullmatch(self.lexical)
        if match is None:
            raise ValueError(f"Invalid date: {self.lexical!r}")
        _validate_date_parts(match.group("year"), match.group("month"), match.group("day"))
        _validate_timezone(match.group("tz"))

    def to_json_value(self) -> str:
        return self.lexical

    def to_xml_text(self) -> str:
        return self.lexical


@dataclass(frozen=True)
class CogsTime:
    lexical: str

    def __post_init__(self) -> None:
        if not isinstance(self.lexical, str):
            raise TypeError("time must be a string.")
        match = _TIME_PATTERN.fullmatch(self.lexical)
        if match is None:
            raise ValueError(f"Invalid time: {self.lexical!r}")
        _validate_time_parts(match)

    def to_json_value(self) -> str:
        return self.lexical

    def to_xml_text(self) -> str:
        return self.lexical


@dataclass(frozen=True)
class CogsDuration:
    lexical: str

    def __post_init__(self) -> None:
        if not isinstance(self.lexical, str) or _DURATION_PATTERN.fullmatch(self.lexical) is None:
            raise ValueError(f"Invalid XML Schema duration: {self.lexical!r}")

    def to_json_value(self) -> str:
        return self.lexical

    def to_xml_text(self) -> str:
        return self.lexical


def _format_year(value: int) -> str:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError("Gregorian years are nonzero integers.")
    _validate_year(str(value))
    sign = "-" if value < 0 else ""
    return sign + f"{abs(value):04d}"


def _gregorian_timezone(value: str | None) -> str:
    _validate_timezone(value)
    return value or ""


def _gregorian_object(
    raw: Any,
    required: tuple[str, ...],
    optional: tuple[str, ...] = ("Timezone",),
) -> dict[str, Any]:
    if not isinstance(raw, dict):
        raise TypeError("Gregorian JSON values must be objects.")
    allowed = set(required) | set(optional)
    unknown = set(raw) - allowed
    missing = set(required) - set(raw)
    if unknown:
        raise ValueError(f"Unknown Gregorian fields: {', '.join(sorted(unknown))}.")
    if missing:
        raise ValueError(f"Missing Gregorian fields: {', '.join(sorted(missing))}.")
    return raw


def _gregorian_integer(raw: Any, name: str) -> int:
    if isinstance(raw, bool) or not isinstance(raw, int):
        raise TypeError(f"{name} must be an integer.")
    return raw


def _gregorian_timezone_value(raw: dict[str, Any]) -> str | None:
    if "Timezone" not in raw:
        return None
    value = raw["Timezone"]
    if not isinstance(value, str):
        raise TypeError("Timezone must be a string.")
    return value


@dataclass(frozen=True, init=False)
class GYearMonth:
    lexical: str
    year: int
    month: int
    timezone: str | None

    def __init__(self, value: str | int, month: int | None = None, timezone: str | None = None) -> None:
        if isinstance(value, str) and month is None and timezone is None:
            lexical = value
        elif (
            isinstance(value, int)
            and not isinstance(value, bool)
            and isinstance(month, int)
            and not isinstance(month, bool)
        ):
            if month < 1 or month > 12:
                raise ValueError("month must be between 1 and 12")
            lexical = f"{_format_year(value)}-{month:02d}{_gregorian_timezone(timezone)}"
        else:
            raise TypeError("GYearMonth requires a lexical string or year, month, and optional timezone.")
        match = re.fullmatch(rf"(?P<year>{_YEAR_PATTERN})-(?P<month>[0-9]{{2}})(?P<tz>Z|[+-][0-9]{{2}}:[0-9]{{2}})?", lexical)
        if match is None:
            raise ValueError(f"Invalid gYearMonth: {lexical!r}")
        year = _validate_year(match.group("year"))
        parsed_month = int(match.group("month"))
        if not 1 <= parsed_month <= 12:
            raise ValueError("month must be between 1 and 12")
        _validate_timezone(match.group("tz"))
        object.__setattr__(self, "lexical", lexical)
        object.__setattr__(self, "year", year)
        object.__setattr__(self, "month", parsed_month)
        object.__setattr__(self, "timezone", match.group("tz"))

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Year": self.year, "Month": self.month}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GYearMonth:
        value = _gregorian_object(raw, ("Year", "Month"))
        return cls(
            _gregorian_integer(value["Year"], "Year"),
            _gregorian_integer(value["Month"], "Month"),
            _gregorian_timezone_value(value),
        )

    def to_xml_text(self) -> str:
        return self.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> GYearMonth:
        return cls(raw)


@dataclass(frozen=True, init=False)
class GYear:
    lexical: str
    year: int
    timezone: str | None

    def __init__(self, value: str | int, timezone: str | None = None) -> None:
        lexical = value if isinstance(value, str) and timezone is None else f"{_format_year(value)}{_gregorian_timezone(timezone)}"
        if not isinstance(lexical, str):
            raise TypeError("GYear requires a lexical string or year and optional timezone.")
        match = re.fullmatch(rf"(?P<year>{_YEAR_PATTERN})(?P<tz>Z|[+-][0-9]{{2}}:[0-9]{{2}})?", lexical)
        if match is None:
            raise ValueError(f"Invalid gYear: {lexical!r}")
        year = _validate_year(match.group("year"))
        _validate_timezone(match.group("tz"))
        object.__setattr__(self, "lexical", lexical)
        object.__setattr__(self, "year", year)
        object.__setattr__(self, "timezone", match.group("tz"))

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Year": self.year}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GYear:
        value = _gregorian_object(raw, ("Year",))
        return cls(
            _gregorian_integer(value["Year"], "Year"),
            _gregorian_timezone_value(value),
        )

    def to_xml_text(self) -> str:
        return self.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> GYear:
        return cls(raw)


@dataclass(frozen=True, init=False)
class GMonthDay:
    lexical: str
    month: int
    day: int
    timezone: str | None

    def __init__(self, value: str | int, day: int | None = None, timezone: str | None = None) -> None:
        if isinstance(value, str) and day is None and timezone is None:
            lexical = value
        elif (
            isinstance(value, int)
            and not isinstance(value, bool)
            and isinstance(day, int)
            and not isinstance(day, bool)
        ):
            lexical = f"--{value:02d}-{day:02d}{_gregorian_timezone(timezone)}"
        else:
            raise TypeError("GMonthDay requires a lexical string or month, day, and optional timezone.")
        match = re.fullmatch(r"--(?P<month>[0-9]{2})-(?P<day>[0-9]{2})(?P<tz>Z|[+-][0-9]{2}:[0-9]{2})?", lexical)
        if match is None:
            raise ValueError(f"Invalid gMonthDay: {lexical!r}")
        _validate_date_parts("2000", match.group("month"), match.group("day"))
        _validate_timezone(match.group("tz"))
        object.__setattr__(self, "lexical", lexical)
        object.__setattr__(self, "month", int(match.group("month")))
        object.__setattr__(self, "day", int(match.group("day")))
        object.__setattr__(self, "timezone", match.group("tz"))

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Month": self.month, "Day": self.day}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GMonthDay:
        value = _gregorian_object(raw, ("Month", "Day"))
        return cls(
            _gregorian_integer(value["Month"], "Month"),
            _gregorian_integer(value["Day"], "Day"),
            _gregorian_timezone_value(value),
        )

    def to_xml_text(self) -> str:
        return self.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> GMonthDay:
        return cls(raw)


@dataclass(frozen=True, init=False)
class GMonth:
    lexical: str
    month: int
    timezone: str | None

    def __init__(self, value: str | int, timezone: str | None = None) -> None:
        if isinstance(value, str) and timezone is None:
            lexical = value
        elif isinstance(value, int) and not isinstance(value, bool):
            lexical = f"--{value:02d}--{_gregorian_timezone(timezone)}"
        else:
            raise TypeError("GMonth requires a lexical string or month and optional timezone.")
        if not isinstance(lexical, str):
            raise TypeError("GMonth requires a lexical string or month and optional timezone.")
        match = re.fullmatch(r"--(?P<month>[0-9]{2})--(?P<tz>Z|[+-][0-9]{2}:[0-9]{2})?", lexical)
        if match is None or not 1 <= int(match.group("month")) <= 12:
            raise ValueError(f"Invalid gMonth: {lexical!r}")
        _validate_timezone(match.group("tz"))
        object.__setattr__(self, "lexical", lexical)
        object.__setattr__(self, "month", int(match.group("month")))
        object.__setattr__(self, "timezone", match.group("tz"))

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Month": self.month}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GMonth:
        value = _gregorian_object(raw, ("Month",))
        return cls(
            _gregorian_integer(value["Month"], "Month"),
            _gregorian_timezone_value(value),
        )

    def to_xml_text(self) -> str:
        return self.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> GMonth:
        return cls(raw)


@dataclass(frozen=True, init=False)
class GDay:
    lexical: str
    day: int
    timezone: str | None

    def __init__(self, value: str | int, timezone: str | None = None) -> None:
        if isinstance(value, str) and timezone is None:
            lexical = value
        elif isinstance(value, int) and not isinstance(value, bool):
            lexical = f"---{value:02d}{_gregorian_timezone(timezone)}"
        else:
            raise TypeError("GDay requires a lexical string or day and optional timezone.")
        if not isinstance(lexical, str):
            raise TypeError("GDay requires a lexical string or day and optional timezone.")
        match = re.fullmatch(r"---(?P<day>[0-9]{2})(?P<tz>Z|[+-][0-9]{2}:[0-9]{2})?", lexical)
        if match is None or not 1 <= int(match.group("day")) <= 31:
            raise ValueError(f"Invalid gDay: {lexical!r}")
        _validate_timezone(match.group("tz"))
        object.__setattr__(self, "lexical", lexical)
        object.__setattr__(self, "day", int(match.group("day")))
        object.__setattr__(self, "timezone", match.group("tz"))

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Day": self.day}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GDay:
        value = _gregorian_object(raw, ("Day",))
        return cls(
            _gregorian_integer(value["Day"], "Day"),
            _gregorian_timezone_value(value),
        )

    def to_xml_text(self) -> str:
        return self.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> GDay:
        return cls(raw)


@dataclass(frozen=True)
class LangString:
    language: str
    value: str

    def __post_init__(self) -> None:
        if not isinstance(self.language, str) or not _is_valid_language(self.language):
            raise ValueError(f"Invalid language tag: {self.language!r}")
        if not isinstance(self.value, str):
            raise TypeError("langString value must be a string.")

    def to_json_value(self) -> dict[str, str]:
        return {"@language": self.language, "@value": self.value}

    @classmethod
    def from_json_value(cls, raw: Any) -> LangString:
        if not isinstance(raw, dict) or set(raw) != {"@language", "@value"}:
            raise ValueError("langString must contain exactly @language and @value.")
        if not isinstance(raw["@language"], str) or not isinstance(raw["@value"], str):
            raise TypeError("langString language and value must be strings.")
        return cls(language=raw["@language"], value=raw["@value"])


@dataclass(frozen=True)
class CogsDate:
    value: CogsDateTime | CogsDateOnly | GYearMonth | GYear | CogsDuration

    def __post_init__(self) -> None:
        if not isinstance(self.value, (CogsDateTime, CogsDateOnly, GYearMonth, GYear, CogsDuration)):
            raise TypeError("CogsDate requires one supported lexical value helper.")

    def to_json_value(self) -> dict[str, Any]:
        if isinstance(self.value, CogsDateTime):
            return {"DateTime": self.value.lexical}
        if isinstance(self.value, CogsDateOnly):
            return {"Date": self.value.lexical}
        if isinstance(self.value, GYearMonth):
            return {"GYearMonth": self.value.to_json_value()}
        if isinstance(self.value, GYear):
            return {"GYear": self.value.to_json_value()}
        return {"Duration": self.value.lexical}

    @classmethod
    def from_json_value(cls, raw: Any) -> CogsDate:
        if not isinstance(raw, dict) or len(raw) != 1:
            raise ValueError("cogsDate must contain exactly one active value.")
        name, value = next(iter(raw.items()))
        if name == "GYearMonth":
            return cls(GYearMonth.from_json_value(value))
        if name == "GYear":
            return cls(GYear.from_json_value(value))
        constructors: dict[str, type[Any]] = {
            "DateTime": CogsDateTime,
            "Date": CogsDateOnly,
            "Duration": CogsDuration,
        }
        if name not in constructors:
            raise ValueError(f"Unknown cogsDate member: {name}")
        if not isinstance(value, str):
            raise TypeError(f"cogsDate {name} must be a lexical string.")
        return cls(constructors[name](value))

    def to_xml_text(self) -> str:
        return self.value.lexical

    @classmethod
    def from_xml_text(cls, raw: str) -> CogsDate:
        constructors: tuple[type[Any], ...]
        if raw.startswith("P") or raw.startswith("-P"):
            constructors = (CogsDuration,)
        elif "T" in raw:
            constructors = (CogsDateTime,)
        elif re.fullmatch(rf"{_YEAR_PATTERN}-[0-9]{{2}}(?:Z|[+-][0-9]{{2}}:[0-9]{{2}})?", raw):
            constructors = (GYearMonth,)
        elif re.fullmatch(rf"{_YEAR_PATTERN}(?:Z|[+-][0-9]{{2}}:[0-9]{{2}})?", raw):
            constructors = (GYear,)
        else:
            constructors = (CogsDateOnly,)
        return cls(constructors[0](raw))


_STRING_TYPES = {"string", "language", "anyuri"}
_INTEGER_TYPES = {
    "nonpositiveinteger", "negativeinteger", "long", "int",
    "nonnegativeinteger", "unsignedlong", "positiveinteger",
}
_FLOAT_TYPES = {"float", "double"}


def _validate_integer(type_name: str, value: Any) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise TypeError(f"{type_name} must be an integer.")
    lowered = type_name.lower()
    valid = {
        "nonpositiveinteger": value <= 0,
        "negativeinteger": value < 0,
        "long": -(2**63) <= value <= 2**63 - 1,
        "int": -(2**31) <= value <= 2**31 - 1,
        "nonnegativeinteger": value >= 0,
        "unsignedlong": 0 <= value <= 2**64 - 1,
        "positiveinteger": value > 0,
    }[lowered]
    if not valid:
        raise ValueError(f"{value} is outside the {type_name} value space.")
    return value


def _validate_float(type_name: str, value: Any) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float, Decimal)):
        raise TypeError(f"{type_name} must be a number.")
    try:
        result = float(value)
    except (OverflowError, ValueError) as exc:
        raise ValueError(f"Invalid {type_name}: {value!r}") from exc
    if not math.isfinite(result):
        raise ValueError(f"{type_name} must be finite.")
    if type_name.lower() == "float" and abs(result) > 3.4028234663852886e38:
        raise ValueError("float is outside the IEEE-754 binary32 finite range.")
    return result


def _serialize_simple_json(type_name: str, value: Any) -> Any:
    lowered = type_name.lower()
    if lowered in _STRING_TYPES:
        if not isinstance(value, str):
            raise TypeError(f"{type_name} must be a string.")
        if lowered == "language" and not _is_valid_language(value):
            raise ValueError(f"Invalid language tag: {value!r}")
        if lowered == "anyuri" and not _is_valid_uri_reference(value):
            raise ValueError(f"Invalid URI reference: {value!r}")
        return value
    if lowered in _INTEGER_TYPES:
        return _validate_integer(type_name, value)
    if lowered in _FLOAT_TYPES:
        return _validate_float(type_name, value)
    if lowered == "boolean":
        if not isinstance(value, bool):
            raise TypeError("boolean must be true or false.")
        return value
    if lowered == "decimal":
        if isinstance(value, CogsDecimal):
            return value
        if isinstance(value, bool) or not isinstance(value, (Decimal, int)):
            raise TypeError("decimal requires CogsDecimal, Decimal, or int.")
        return CogsDecimal(value)
    lexical_helper_types: dict[str, type[Any]] = {
        "datetime": CogsDateTime,
        "date": CogsDateOnly,
        "time": CogsTime,
        "duration": CogsDuration,
    }
    gregorian_helper_types: dict[str, type[Any]] = {
        "gyearmonth": GYearMonth,
        "gyear": GYear,
        "gmonthday": GMonthDay,
        "gmonth": GMonth,
        "gday": GDay,
    }
    if lowered in lexical_helper_types:
        if not isinstance(value, lexical_helper_types[lowered]):
            raise TypeError(f"{type_name} requires {lexical_helper_types[lowered].__name__}.")
        return value.lexical
    if lowered in gregorian_helper_types:
        if not isinstance(value, gregorian_helper_types[lowered]):
            raise TypeError(f"{type_name} requires {gregorian_helper_types[lowered].__name__}.")
        return value.to_json_value()
    if lowered == "langstring":
        if not isinstance(value, LangString):
            raise TypeError("langString requires LangString.")
        return value.to_json_value()
    if lowered == "cogsdate":
        if not isinstance(value, CogsDate):
            raise TypeError("cogsDate requires CogsDate.")
        return value.to_json_value()
    raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _deserialize_simple_json(type_name: str, raw: Any) -> Any:
    lowered = type_name.lower()
    if lowered in _STRING_TYPES:
        if not isinstance(raw, str):
            raise TypeError(f"{type_name} must be a string.")
        if lowered == "language" and not _is_valid_language(raw):
            raise ValueError(f"Invalid language tag: {raw!r}")
        if lowered == "anyuri" and not _is_valid_uri_reference(raw):
            raise ValueError(f"Invalid URI reference: {raw!r}")
        return raw
    if lowered in _INTEGER_TYPES:
        return _validate_integer(type_name, raw)
    if lowered in _FLOAT_TYPES:
        return _validate_float(type_name, raw)
    if lowered == "decimal":
        if isinstance(raw, CogsDecimal):
            return raw
        if isinstance(raw, bool) or not isinstance(raw, (int, Decimal)):
            raise TypeError("decimal must be an exact JSON number or CogsDecimal.")
        return CogsDecimal(raw)
    if lowered == "boolean":
        if not isinstance(raw, bool):
            raise TypeError("boolean must be true or false.")
        return raw
    lexical_constructors: dict[str, type[Any]] = {
        "datetime": CogsDateTime,
        "date": CogsDateOnly,
        "time": CogsTime,
        "duration": CogsDuration,
    }
    gregorian_constructors: dict[str, type[Any]] = {
        "gyearmonth": GYearMonth,
        "gyear": GYear,
        "gmonthday": GMonthDay,
        "gmonth": GMonth,
        "gday": GDay,
    }
    if lowered in lexical_constructors:
        if isinstance(raw, lexical_constructors[lowered]):
            return raw
        if not isinstance(raw, str):
            raise TypeError(f"{type_name} must be a lexical string.")
        return lexical_constructors[lowered](raw)
    if lowered in gregorian_constructors:
        if isinstance(raw, gregorian_constructors[lowered]):
            return raw
        return gregorian_constructors[lowered].from_json_value(raw)
    if lowered == "langstring":
        return raw if isinstance(raw, LangString) else LangString.from_json_value(raw)
    if lowered == "cogsdate":
        return raw if isinstance(raw, CogsDate) else CogsDate.from_json_value(raw)
    raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _serialize_simple_xml(type_name: str, value: Any, element: ET.Element) -> None:
    lowered = type_name.lower()
    if lowered == "langstring":
        if not isinstance(value, LangString):
            raise TypeError("langString requires LangString.")
        element.text = value.value
        element.set(f"{{{XML_NAMESPACE}}}lang", value.language)
        return
    xml_helper_types: dict[str, type[Any]] = {
        "datetime": CogsDateTime,
        "date": CogsDateOnly,
        "time": CogsTime,
        "duration": CogsDuration,
        "gyearmonth": GYearMonth,
        "gyear": GYear,
        "gmonthday": GMonthDay,
        "gmonth": GMonth,
        "gday": GDay,
    }
    if lowered in xml_helper_types:
        if not isinstance(value, xml_helper_types[lowered]):
            raise TypeError(f"{type_name} requires {xml_helper_types[lowered].__name__}.")
        element.text = value.to_xml_text()
        return
    if lowered == "cogsdate":
        if not isinstance(value, CogsDate):
            raise TypeError("cogsDate requires CogsDate.")
        element.text = value.to_xml_text()
        return
    serialized = _serialize_simple_json(type_name, value)
    if lowered == "boolean":
        element.text = "true" if serialized else "false"
    elif lowered == "decimal":
        element.text = serialized.lexical
    else:
        element.text = str(serialized)


def _deserialize_simple_xml(type_name: str, element: ET.Element) -> Any:
    lowered = type_name.lower()
    if len(element):
        raise ValueError(f"{type_name} cannot contain child elements.")
    raw = element.text or ""
    if lowered == "langstring":
        unknown_attributes = set(element.attrib) - {f"{{{XML_NAMESPACE}}}lang"}
        if unknown_attributes:
            raise ValueError("langString contains unknown XML attributes.")
        language = element.get(f"{{{XML_NAMESPACE}}}lang")
        if language is None:
            raise ValueError("langString requires xml:lang.")
        return LangString(language=language.strip(), value=raw)
    if element.attrib:
        raise ValueError(f"{type_name} contains unknown XML attributes.")
    if lowered == "string":
        return _deserialize_simple_json(type_name, raw)
    raw = raw.strip()
    if lowered in _STRING_TYPES:
        return _deserialize_simple_json(type_name, raw)
    if lowered in _INTEGER_TYPES:
        if re.fullmatch(r"[+-]?[0-9]+", raw) is None:
            raise ValueError(f"Invalid {type_name}: {raw!r}")
        return _validate_integer(type_name, int(raw))
    if lowered == "decimal":
        return _decimal_from_xml(raw)
    if lowered in _FLOAT_TYPES:
        try:
            parsed = Decimal(raw)
        except InvalidOperation as exc:
            raise ValueError(f"Invalid {type_name}: {raw!r}") from exc
        return _validate_float(type_name, parsed)
    if lowered == "boolean":
        if raw in {"true", "1"}:
            return True
        if raw in {"false", "0"}:
            return False
        raise ValueError(f"Invalid boolean: {raw!r}")
    constructors: dict[str, type[Any]] = {
        "datetime": CogsDateTime,
        "date": CogsDateOnly,
        "time": CogsTime,
        "duration": CogsDuration,
        "gyearmonth": GYearMonth,
        "gyear": GYear,
        "gmonthday": GMonthDay,
        "gmonth": GMonth,
        "gday": GDay,
    }
    if lowered in constructors:
        return constructors[lowered](raw)
    if lowered == "cogsdate":
        return CogsDate.from_xml_text(raw)
    raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _field_by_wire_name(cls: type[CogsValue]) -> dict[str, Any]:
    return {item.metadata["cogs_name"]: item for item in fields(cls)}


def _type_for_name(type_name: str) -> type[CogsValue]:
    try:
        return TYPE_REGISTRY[type_name]
    except KeyError as exc:
        raise ValueError(f"Unknown COGS type: {type_name}") from exc


IdentityKey = tuple[str, tuple[str, ...]]


class _Context:
    def __init__(
        self,
        namespaces: Mapping[str, str] | None = None,
        element_namespaces: Mapping[int, Mapping[str, str]] | None = None,
    ) -> None:
        self.items_by_key: dict[IdentityKey, CogsItem] = {}
        self.defined_keys: set[IdentityKey] = set()
        self.namespaces = _namespace_map(namespaces)
        self.element_namespaces = {
            key: _namespace_map(value) for key, value in (element_namespaces or {}).items()
        }

    def namespaces_for(self, element: ET.Element) -> dict[str, str]:
        return self.element_namespaces.get(id(element), self.namespaces)

    def _reference_info(
        self,
        raw: Any,
        expected_type: str | None = None,
        allow_subtypes: bool = True,
    ) -> tuple[type[CogsItem], IdentityKey, dict[str, str]]:
        if not isinstance(raw, dict):
            raise TypeError("Item references must be objects.")
        allowed = {"$type"} | {wire for wire, _ in IDENTIFICATION_FIELDS}
        unknown = set(raw) - allowed
        missing = allowed - set(raw)
        if unknown:
            raise ValueError(f"Unknown reference fields: {', '.join(sorted(unknown))}")
        if missing:
            raise ValueError(f"Reference is missing fields: {', '.join(sorted(missing))}")
        type_name = raw["$type"]
        if not isinstance(type_name, str):
            raise TypeError("Item reference $type must be a string.")
        try:
            actual_cls = ITEM_TYPE_REGISTRY[type_name]
        except KeyError as exc:
            raise ValueError(f"Unknown item type: {type_name}") from exc
        if actual_cls._is_abstract:
            raise ValueError(f"Abstract item type cannot be instantiated: {type_name}")
        if expected_type is not None:
            try:
                expected_cls = ITEM_TYPE_REGISTRY[expected_type]
            except KeyError as exc:
                raise ValueError(f"Unknown declared item type: {expected_type}") from exc
            if not issubclass(actual_cls, expected_cls) or (
                not allow_subtypes and actual_cls is not expected_cls
            ):
                if allow_subtypes:
                    raise TypeError(f"{type_name} is not assignable to {expected_type}.")
                raise TypeError(
                    f"Item reference {expected_type} requires the exact type; found {type_name}."
                )
        by_wire = _field_by_wire_name(actual_cls)
        parsed: dict[str, str] = {}
        for wire_name, _ in IDENTIFICATION_FIELDS:
            item = by_wire.get(wire_name)
            if item is None or item.metadata["kind"] != "simple":
                raise ValueError(f"Item type {type_name} has no scalar identity field {wire_name}.")
            value = _deserialize_simple_json(item.metadata["type_name"], raw[wire_name])
            if not isinstance(value, str):
                raise TypeError(f"Identity field {wire_name} must deserialize to a string.")
            if value == "":
                raise ValueError(f"Identity field {wire_name} must be nonempty.")
            parsed[wire_name] = value
        key: IdentityKey = (type_name, tuple(parsed[wire] for wire, _ in IDENTIFICATION_FIELDS))
        return actual_cls, key, parsed

    def resolve_reference(
        self,
        raw: Any,
        expected_type: str | None = None,
        allow_subtypes: bool = True,
    ) -> CogsItem:
        actual_cls, key, parsed = self._reference_info(raw, expected_type, allow_subtypes)
        item = self.items_by_key.get(key)
        if item is None:
            item = actual_cls()
            item._cogs_is_defined = False
            by_wire = _field_by_wire_name(actual_cls)
            for wire_name, attribute_name in IDENTIFICATION_FIELDS:
                setattr(item, attribute_name, parsed[wire_name])
            self.items_by_key[key] = item
        return item

    def predeclare_item(self, raw: Any) -> CogsItem:
        if not isinstance(raw, dict) or "$type" not in raw:
            raise ValueError("Serialized items require a $type discriminator.")
        type_name = raw["$type"]
        if not isinstance(type_name, str):
            raise TypeError("Serialized item $type must be a string.")
        try:
            actual_cls = ITEM_TYPE_REGISTRY[type_name]
        except KeyError as exc:
            raise ValueError(f"Unknown item type: {type_name}") from exc
        allowed = {"$type"} | set(_field_by_wire_name(actual_cls))
        unknown = set(raw) - allowed
        if unknown:
            raise ValueError(f"Unknown fields for {type_name}: {', '.join(sorted(unknown))}")
        reference = {"$type": type_name}
        for wire_name, _ in IDENTIFICATION_FIELDS:
            if wire_name not in raw:
                raise ValueError(f"Item {type_name} is missing identification field {wire_name}.")
            reference[wire_name] = raw[wire_name]
        item = self.resolve_reference(reference, type_name)
        _, key, _ = self._reference_info(reference, type_name)
        if key in self.defined_keys:
            raise ValueError(f"Duplicate full item definition: {type_name} {key[1]!r}")
        self.defined_keys.add(key)
        item._cogs_is_defined = True
        return item

    def populate_item(self, raw: dict[str, Any]) -> CogsItem:
        type_name = raw["$type"]
        reference = {"$type": type_name}
        for wire_name, _ in IDENTIFICATION_FIELDS:
            reference[wire_name] = raw[wire_name]
        item = self.resolve_reference(reference, type_name)
        item._populate_from_dict(raw, self, type_field_allowed=True, type_field_required=True)
        return item

    def register_definition(self, item: CogsItem) -> None:
        reference = item.to_reference_dict()
        _, key, _ = self._reference_info(reference, item._cogs_type)
        if key in self.defined_keys:
            raise ValueError(f"Duplicate full item definition: {item._cogs_type} {key[1]!r}")
        self.defined_keys.add(key)
        existing = self.items_by_key.get(key)
        if existing is not None and existing is not item:
            raise ValueError(f"Distinct objects share item identity {item._cogs_type} {key[1]!r}.")
        self.items_by_key[key] = item
        item._cogs_is_defined = True


@dataclass
class CogsValue:
    _cogs_type: ClassVar[str] = ""
    _is_item: ClassVar[bool] = False
    _is_abstract: ClassVar[bool] = False

    def _to_dict_with_context(self, context: _Context, *, include_type: bool = False) -> dict[str, Any]:
        if self._is_abstract:
            raise TypeError(f"Abstract type cannot be serialized: {self._cogs_type}")
        result: dict[str, Any] = {}
        if include_type:
            result["$type"] = self._cogs_type
        for item in fields(self):
            value = getattr(self, item.name)
            if value is None or (item.metadata["many"] and not value):
                continue
            result[item.metadata["cogs_name"]] = _serialize_field_json(value, item.metadata, context)
        return result

    def to_dict(self) -> dict[str, Any]:
        return self._to_dict_with_context(_Context())

    def to_json(self, *, indent: int | None = None) -> str:
        if indent is not None and indent < 0:
            raise ValueError("indent cannot be negative")
        return _json_dump_value(self.to_dict(), indent)

    def _populate_from_dict(
        self,
        data: Any,
        context: _Context,
        *,
        type_field_allowed: bool = False,
        type_field_required: bool = False,
    ) -> None:
        if not isinstance(data, dict):
            raise TypeError(f"{self._cogs_type} must be a JSON object.")
        by_wire = _field_by_wire_name(type(self))
        allowed = set(by_wire)
        if type_field_allowed:
            allowed.add("$type")
        unknown = set(data) - allowed
        if unknown:
            raise ValueError(f"Unknown fields for {self._cogs_type}: {', '.join(sorted(unknown))}")
        if type_field_required and "$type" not in data:
            raise ValueError(f"{self._cogs_type} requires a $type discriminator.")
        if "$type" in data:
            if not type_field_allowed:
                raise ValueError(f"$type is not allowed for exact {self._cogs_type} values.")
            if data["$type"] != self._cogs_type:
                raise ValueError(f"Expected $type {self._cogs_type}, got {data['$type']!r}.")
        for wire_name, item in by_wire.items():
            if wire_name in data:
                setattr(self, item.name, _deserialize_field_json(data[wire_name], item.metadata, context))

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> CogsValue:
        if not isinstance(data, dict):
            raise TypeError(f"{cls.__name__} must be a JSON object.")
        context = _Context()
        if issubclass(cls, CogsItem):
            item = context.predeclare_item(data)
            result = context.populate_item(data)
            if result is not item or not isinstance(result, cls):
                raise TypeError(f"{type(result).__name__} is not assignable to {cls.__name__}.")
            return result
        target_cls = cls
        has_type = "$type" in data
        if has_type:
            if not isinstance(data["$type"], str):
                raise TypeError("$type must be a string.")
            candidate = _type_for_name(data["$type"])
            if issubclass(candidate, CogsItem) or not issubclass(candidate, cls) or candidate._is_abstract:
                raise TypeError(f"{candidate.__name__} is not assignable to {cls.__name__}.")
            target_cls = candidate
        if target_cls._is_abstract:
            raise ValueError(f"Abstract type {target_cls.__name__} requires $type.")
        instance = target_cls()
        instance._populate_from_dict(
            data,
            context,
            type_field_allowed=has_type,
            type_field_required=has_type,
        )
        return instance

    @classmethod
    def from_json(cls, value: str | bytes | bytearray) -> CogsValue:
        return cls.from_dict(_json_load_value(value))

    def _to_element_with_context(
        self,
        element_name: str,
        context: _Context,
        declared_type: str | None = None,
        allow_subtypes: bool = False,
    ) -> ET.Element:
        if self._is_abstract:
            raise TypeError(f"Abstract type cannot be serialized: {self._cogs_type}")
        element = ET.Element(_q(element_name))
        if declared_type is not None:
            declared_cls = _type_for_name(declared_type)
            if isinstance(self, CogsItem) or issubclass(declared_cls, CogsItem):
                raise TypeError("xsi:type is only valid at composite-valued properties.")
            if not isinstance(self, declared_cls):
                raise TypeError(f"{self._cogs_type} is not assignable to {declared_type}.")
            if type(self) is not declared_cls and not allow_subtypes:
                raise TypeError(f"Subtypes are not allowed where {declared_type} is declared.")
            if allow_subtypes:
                element.set(f"{{{XSI_NAMESPACE}}}type", f"{NAMESPACE_PREFIX}:{self._cogs_type}")
        for item in fields(self):
            value = getattr(self, item.name)
            if value is None or (item.metadata["many"] and not value):
                continue
            values = value if item.metadata["many"] else [value]
            for child_value in values:
                element.append(_serialize_field_xml(child_value, item.metadata, context))
        return element

    def to_element(self, element_name: str | None = None) -> ET.Element:
        element = self._to_element_with_context(element_name or self._cogs_type, _Context())
        if any(f"{{{XSI_NAMESPACE}}}type" in child.attrib for child in element.iter()):
            element.set(f"xmlns:{NAMESPACE_PREFIX}", TARGET_NAMESPACE)
        return element

    def to_xml(self, element_name: str | None = None, *, xml_declaration: bool = False) -> str:
        return ET.tostring(
            self.to_element(element_name),
            encoding="utf-8",
            xml_declaration=xml_declaration,
            short_empty_elements=True,
        ).decode("utf-8")

    @classmethod
    def from_element(
        cls,
        element: ET.Element,
        *,
        namespaces: Mapping[str, str] | None = None,
        allow_subtypes: bool = False,
        _element_namespaces: Mapping[int, Mapping[str, str]] | None = None,
    ) -> CogsValue:
        context = _Context(namespaces, _element_namespaces)
        element_type = _model_local_name(element)
        if issubclass(cls, CogsItem):
            try:
                target_cls = ITEM_TYPE_REGISTRY[element_type]
            except KeyError as exc:
                raise ValueError(f"Unknown item element: {element_type}") from exc
            if target_cls._is_abstract:
                raise ValueError(f"Abstract item type cannot be instantiated: {element_type}")
            if not issubclass(target_cls, cls):
                raise TypeError(f"{target_cls.__name__} is not assignable to {cls.__name__}.")
            reference = _item_reference_from_element(element, element_type)
            instance = context.resolve_reference(reference, element_type)
            _, key, _ = context._reference_info(reference, element_type)
            context.defined_keys.add(key)
            instance._populate_from_element(element, context)
            return instance
        target_cls = _target_class_from_element(
            cls, element, allow_subtypes, context.namespaces_for(element)
        )
        instance = target_cls()
        instance._populate_from_element(
            element,
            context,
            xsi_type_allowed=allow_subtypes and f"{{{XSI_NAMESPACE}}}type" in element.attrib,
        )
        return instance

    @classmethod
    def from_xml(cls, value: str | bytes | bytearray, *, allow_subtypes: bool = False) -> CogsValue:
        element, namespaces, element_namespaces = _parse_xml(value)
        return cls.from_element(
            element,
            namespaces=namespaces,
            allow_subtypes=allow_subtypes,
            _element_namespaces=element_namespaces,
        )

    def _populate_from_element(
        self,
        element: ET.Element,
        context: _Context,
        *,
        xsi_type_allowed: bool = False,
    ) -> None:
        _model_local_name(element)
        allowed_attributes: set[str] = set()
        if xsi_type_allowed:
            allowed_attributes.add(f"{{{XSI_NAMESPACE}}}type")
        namespace_declaration = f"xmlns:{NAMESPACE_PREFIX}"
        if element.get(namespace_declaration) == TARGET_NAMESPACE:
            allowed_attributes.add(namespace_declaration)
        unknown_attributes = set(element.attrib) - allowed_attributes
        if unknown_attributes:
            raise ValueError(f"Unknown XML attributes for {self._cogs_type}: {sorted(unknown_attributes)!r}.")
        _check_no_mixed_content(element, self._cogs_type)
        by_wire = _field_by_wire_name(type(self))
        positions = {name: index for index, name in enumerate(by_wire)}
        grouped: dict[str, list[ET.Element]] = {name: [] for name in by_wire}
        last_position = -1
        for child in element:
            name = _model_local_name(child)
            if child.tag != _q(name) or name not in by_wire:
                raise ValueError(f"Unknown XML element for {self._cogs_type}: {name}")
            position = positions[name]
            if position < last_position:
                raise ValueError(f"XML element {name} is out of schema order for {self._cogs_type}.")
            last_position = position
            grouped[name].append(child)
            if not by_wire[name].metadata["many"] and len(grouped[name]) > 1:
                raise ValueError(f"{name} occurs more than once.")
        for wire_name, item in by_wire.items():
            matches = grouped[wire_name]
            if item.metadata["many"]:
                setattr(
                    self,
                    item.name,
                    [_deserialize_field_xml(child, item.metadata, context) for child in matches],
                )
            elif matches:
                setattr(self, item.name, _deserialize_field_xml(matches[0], item.metadata, context))


@dataclass
class CogsItem(CogsValue):
    _is_item: ClassVar[bool] = True

    @property
    def is_defined(self) -> bool:
        """Whether this item was populated by a full definition in its container."""
        return getattr(self, "_cogs_is_defined", False)

    def _to_dict_with_context(self, context: _Context, *, include_type: bool = True) -> dict[str, Any]:
        return super()._to_dict_with_context(context, include_type=True)

    def to_reference_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {"$type": self._cogs_type}
        by_wire = _field_by_wire_name(type(self))
        for wire_name, attribute_name in IDENTIFICATION_FIELDS:
            item = by_wire.get(wire_name)
            if item is None:
                raise ValueError(f"Item type {self._cogs_type} has no identity field {wire_name}.")
            value = getattr(self, attribute_name)
            if value is None:
                raise ValueError(f"Reference field {wire_name} is not set.")
            serialized = _serialize_field_json(value, item.metadata, _Context())
            if not isinstance(serialized, str):
                raise TypeError(f"Reference field {wire_name} must serialize as a string.")
            if serialized == "":
                raise ValueError(f"Reference field {wire_name} must be nonempty.")
            result[wire_name] = serialized
        return result


def _serialize_field_json(value: Any, metadata: Any, context: _Context) -> Any:
    if metadata["many"]:
        if not isinstance(value, list):
            raise TypeError(f"{metadata['cogs_name']} must be a list.")
        return [_serialize_single_json(item, metadata, context) for item in value]
    return _serialize_single_json(value, metadata, context)


def _serialize_single_json(value: Any, metadata: Any, context: _Context) -> Any:
    kind = metadata["kind"]
    if kind == "simple":
        return _serialize_simple_json(metadata["type_name"], value)
    if kind == "item":
        if not isinstance(value, CogsItem):
            raise TypeError(f"{metadata['cogs_name']} requires an item reference.")
        expected = ITEM_TYPE_REGISTRY[metadata["type_name"]]
        if (
            not isinstance(value, expected)
            or value._is_abstract
            or (not metadata["allow_subtypes"] and type(value) is not expected)
        ):
            raise TypeError(f"Invalid item type for {metadata['cogs_name']}.")
        return value.to_reference_dict()
    expected = TYPE_REGISTRY[metadata["type_name"]]
    if not isinstance(value, expected) or isinstance(value, CogsItem):
        raise TypeError(f"Invalid object type for {metadata['cogs_name']}.")
    if metadata["allow_subtypes"]:
        if value._is_abstract:
            raise TypeError(f"Abstract value is invalid for {metadata['cogs_name']}.")
        return value._to_dict_with_context(context, include_type=True)
    if type(value) is not expected:
        raise TypeError(f"Subtypes are not allowed for {metadata['cogs_name']}.")
    return value._to_dict_with_context(context, include_type=False)


def _deserialize_field_json(raw: Any, metadata: Any, context: _Context) -> Any:
    if metadata["many"]:
        if not isinstance(raw, list):
            raise TypeError(f"{metadata['cogs_name']} must be an array.")
        return [_deserialize_single_json(item, metadata, context) for item in raw]
    return _deserialize_single_json(raw, metadata, context)


def _deserialize_single_json(raw: Any, metadata: Any, context: _Context) -> Any:
    kind = metadata["kind"]
    if kind == "simple":
        return _deserialize_simple_json(metadata["type_name"], raw)
    if kind == "item":
        return context.resolve_reference(
            raw, metadata["type_name"], metadata["allow_subtypes"]
        )
    if not isinstance(raw, dict):
        raise TypeError(f"{metadata['cogs_name']} must be an object.")
    declared_cls = TYPE_REGISTRY[metadata["type_name"]]
    if metadata["allow_subtypes"]:
        if "$type" not in raw or not isinstance(raw["$type"], str):
            raise ValueError(f"{metadata['cogs_name']} requires a string $type discriminator.")
        candidate = _type_for_name(raw["$type"])
        if issubclass(candidate, CogsItem):
            raise TypeError("Composite discriminators cannot name item types.")
        if not issubclass(candidate, declared_cls) or candidate._is_abstract:
            raise TypeError(f"{candidate.__name__} is not assignable to {declared_cls.__name__}.")
        target_cls = candidate
        type_allowed = True
    else:
        if "$type" in raw:
            raise ValueError(f"$type is forbidden for exact {metadata['cogs_name']} values.")
        if declared_cls._is_abstract:
            raise ValueError(f"Abstract type {declared_cls.__name__} requires AllowSubtypes.")
        target_cls = declared_cls
        type_allowed = False
    instance = target_cls()
    instance._populate_from_dict(
        raw,
        context,
        type_field_allowed=type_allowed,
        type_field_required=type_allowed,
    )
    return instance


def _serialize_field_xml(value: Any, metadata: Any, context: _Context) -> ET.Element:
    kind = metadata["kind"]
    if kind == "simple":
        element = ET.Element(_q(metadata["cogs_name"]))
        _serialize_simple_xml(metadata["type_name"], value, element)
        return element
    if kind == "item":
        if not isinstance(value, CogsItem):
            raise TypeError(f"{metadata['cogs_name']} requires an item reference.")
        expected = ITEM_TYPE_REGISTRY[metadata["type_name"]]
        if (
            not isinstance(value, expected)
            or value._is_abstract
            or (not metadata["allow_subtypes"] and type(value) is not expected)
        ):
            raise TypeError(f"Invalid item type for {metadata['cogs_name']}.")
        element = ET.Element(_q(metadata["cogs_name"]))
        element.set("isReference", "true")
        reference = value.to_reference_dict()
        value_fields = _field_by_wire_name(type(value))
        for wire_name, _ in IDENTIFICATION_FIELDS:
            child = ET.SubElement(element, _q(wire_name))
            _serialize_simple_xml(
                value_fields[wire_name].metadata["type_name"],
                getattr(value, value_fields[wire_name].name),
                child,
            )
        ET.SubElement(element, _q("TypeOfObject")).text = reference["$type"]
        return element
    if not isinstance(value, CogsValue) or isinstance(value, CogsItem):
        raise TypeError(f"{metadata['cogs_name']} requires a composite COGS value.")
    return value._to_element_with_context(
        metadata["cogs_name"],
        context,
        metadata["type_name"],
        metadata["allow_subtypes"],
    )


def _deserialize_field_xml(element: ET.Element, metadata: Any, context: _Context) -> Any:
    if element.tag != _q(metadata["cogs_name"]):
        raise ValueError(f"Expected qualified element {metadata['cogs_name']}.")
    kind = metadata["kind"]
    if kind == "simple":
        return _deserialize_simple_xml(metadata["type_name"], element)
    if kind == "item":
        return context.resolve_reference(
            _reference_dict_from_element(element),
            metadata["type_name"],
            metadata["allow_subtypes"],
        )
    target_cls = _target_class_from_element(
        TYPE_REGISTRY[metadata["type_name"]],
        element,
        metadata["allow_subtypes"],
        context.namespaces_for(element),
    )
    instance = target_cls()
    instance._populate_from_element(
        element,
        context,
        xsi_type_allowed=f"{{{XSI_NAMESPACE}}}type" in element.attrib,
    )
    return instance


def _target_class_from_element(
    declared_cls: type[CogsValue],
    element: ET.Element,
    allow_subtypes: bool,
    namespaces: Mapping[str, str],
) -> type[CogsValue]:
    xsi_type = element.get(f"{{{XSI_NAMESPACE}}}type")
    if xsi_type is None:
        if allow_subtypes:
            raise ValueError(f"Composite type {declared_cls.__name__} requires a qualified xsi:type.")
        if declared_cls._is_abstract:
            raise ValueError(f"Abstract type {declared_cls.__name__} requires xsi:type.")
        return declared_cls
    if issubclass(declared_cls, CogsItem):
        raise ValueError("xsi:type is forbidden on item definitions and references.")
    if not allow_subtypes:
        raise ValueError(f"xsi:type is not allowed for {declared_cls.__name__}.")
    if xsi_type.count(":") != 1:
        raise ValueError("xsi:type must be a qualified QName.")
    prefix, type_name = xsi_type.split(":", 1)
    if not prefix or not type_name:
        raise ValueError("xsi:type must be a qualified QName.")
    if namespaces.get(prefix) != TARGET_NAMESPACE:
        raise ValueError(f"xsi:type prefix {prefix!r} does not identify the model namespace.")
    candidate = _type_for_name(type_name)
    if issubclass(candidate, CogsItem) or not issubclass(candidate, declared_cls) or candidate._is_abstract:
        raise TypeError(f"Invalid xsi:type {xsi_type} for {declared_cls.__name__}.")
    return candidate


def _reference_dict_from_element(element: ET.Element) -> dict[str, Any]:
    marker = element.attrib.get("isReference")
    if any(name != "isReference" for name in element.attrib):
        raise ValueError("XML references can contain only the unqualified isReference attribute.")
    if marker is not None and marker not in {"true", "1"}:
        raise ValueError(
            "The unqualified isReference attribute must have the fixed boolean value "
            "true (lexically 'true' or '1')."
        )
    _check_no_mixed_content(element, "XML reference")
    children = list(element)
    expected_names = [wire for wire, _ in IDENTIFICATION_FIELDS] + ["TypeOfObject"]
    actual_names = [_model_local_name(child) for child in children]
    if any(child.tag != _q(name) for child, name in zip(children, actual_names)):
        raise ValueError("XML reference elements must use the model namespace.")
    if actual_names != expected_names:
        raise ValueError(
            f"XML reference fields must be ordered as {expected_names!r}; got {actual_names!r}."
        )
    type_element = children[-1]
    if type_element.attrib or len(type_element):
        raise ValueError("TypeOfObject must be simple text without attributes.")
    type_name = type_element.text or ""
    try:
        target_cls = ITEM_TYPE_REGISTRY[type_name]
    except KeyError as exc:
        raise ValueError(f"Unknown item type: {type_name}") from exc
    if target_cls._is_abstract:
        raise ValueError(f"Abstract item type cannot be referenced: {type_name}")
    by_wire = _field_by_wire_name(target_cls)
    result: dict[str, Any] = {"$type": type_name}
    for index, (wire_name, _) in enumerate(IDENTIFICATION_FIELDS):
        result[wire_name] = _deserialize_simple_xml(
            by_wire[wire_name].metadata["type_name"], children[index]
        )
    return result


def _item_reference_from_element(element: ET.Element, type_name: str) -> dict[str, Any]:
    target_cls = ITEM_TYPE_REGISTRY[type_name]
    by_wire = _field_by_wire_name(target_cls)
    grouped: dict[str, list[ET.Element]] = {}
    for child in element:
        name = _model_local_name(child)
        if child.tag != _q(name):
            raise ValueError(f"Unexpected XML namespace on {name}.")
        grouped.setdefault(name, []).append(child)
    reference: dict[str, Any] = {"$type": type_name}
    for wire_name, _ in IDENTIFICATION_FIELDS:
        matches = grouped.get(wire_name, [])
        if len(matches) != 1:
            raise ValueError(f"Item {type_name} requires exactly one {wire_name}.")
        reference[wire_name] = _deserialize_simple_xml(
            by_wire[wire_name].metadata["type_name"], matches[0]
        )
    return reference


@dataclass
class ItemContainer:
    items: list[CogsItem] = field(default_factory=list)
    top_level_references: list[CogsItem] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        context = _Context()
        for item in self.items:
            if not isinstance(item, CogsItem) or item._is_abstract:
                raise TypeError("ItemContainer items must be concrete COGS items.")
            context.register_definition(item)
        result: dict[str, Any] = {}
        if self.top_level_references:
            result["topLevelReferences"] = [item.to_reference_dict() for item in self.top_level_references]
        result["items"] = [item._to_dict_with_context(context) for item in self.items]
        return result

    @classmethod
    def from_dict(cls, data: Any) -> ItemContainer:
        if not isinstance(data, dict):
            raise TypeError("ItemContainer must be an object.")
        unknown = set(data) - {"topLevelReferences", "items"}
        if unknown:
            raise ValueError(f"Unknown ItemContainer fields: {', '.join(sorted(unknown))}")
        if "items" not in data or not isinstance(data["items"], list):
            raise ValueError("ItemContainer requires an items array.")
        references = data.get("topLevelReferences", [])
        if not isinstance(references, list):
            raise TypeError("topLevelReferences must be an array.")
        context = _Context()
        for raw in data["items"]:
            context.predeclare_item(raw)
        container = cls()
        for raw in data["items"]:
            container.items.append(context.populate_item(raw))
        for raw in references:
            container.top_level_references.append(context.resolve_reference(raw))
        return container

    def to_json(self, *, indent: int | None = None) -> str:
        if indent is not None and indent < 0:
            raise ValueError("indent cannot be negative")
        return _json_dump_value(self.to_dict(), indent)

    @classmethod
    def from_json(cls, value: str | bytes | bytearray) -> ItemContainer:
        return cls.from_dict(_json_load_value(value))

    @classmethod
    def load_json(cls, source: str | os.PathLike[str] | IO[str] | IO[bytes]) -> ItemContainer:
        if hasattr(source, "read"):
            return cls.from_json(source.read())
        return cls.from_json(Path(source).read_bytes())

    def dump_json(
        self,
        target: str | os.PathLike[str] | IO[str] | IO[bytes],
        *,
        indent: int | None = 2,
    ) -> None:
        value = self.to_json(indent=indent)
        if hasattr(target, "write"):
            try:
                target.write(value)
            except TypeError:
                target.write(value.encode("utf-8"))
            return
        Path(target).write_text(value, encoding="utf-8", newline="\n")

    def to_element(self) -> ET.Element:
        context = _Context()
        for item in self.items:
            if not isinstance(item, CogsItem) or item._is_abstract:
                raise TypeError("ItemContainer items must be concrete COGS items.")
            context.register_definition(item)
        root = ET.Element(_q("ItemContainer"))
        root.set(f"xmlns:{NAMESPACE_PREFIX}", TARGET_NAMESPACE)
        for item in self.top_level_references:
            metadata = {
                "cogs_name": "TopLevelReference",
                "kind": "item",
                "type_name": item._cogs_type,
                "allow_subtypes": True,
                "many": True,
            }
            root.append(_serialize_field_xml(item, metadata, context))
        for item in self.items:
            root.append(item._to_element_with_context(item._cogs_type, context))
        return root

    @classmethod
    def from_element(
        cls,
        root: ET.Element,
        *,
        namespaces: Mapping[str, str] | None = None,
        _element_namespaces: Mapping[int, Mapping[str, str]] | None = None,
    ) -> ItemContainer:
        if root.tag != _q("ItemContainer"):
            raise ValueError("Expected a namespace-qualified ItemContainer root element.")
        namespace_declaration = f"xmlns:{NAMESPACE_PREFIX}"
        allowed_attributes = {namespace_declaration} if root.get(namespace_declaration) == TARGET_NAMESPACE else set()
        if set(root.attrib) - allowed_attributes:
            raise ValueError("ItemContainer cannot contain XML attributes.")
        _check_no_mixed_content(root, "ItemContainer")
        context = _Context(namespaces, _element_namespaces)
        top_level: list[ET.Element] = []
        item_elements: list[ET.Element] = []
        seen_items = False
        for child in root:
            name = _model_local_name(child)
            if child.tag != _q(name):
                raise ValueError(f"Unexpected XML namespace on {name}.")
            if name == "TopLevelReference":
                if seen_items:
                    raise ValueError("TopLevelReference elements must precede items.")
                top_level.append(child)
            else:
                seen_items = True
                if name not in ITEM_TYPE_REGISTRY:
                    raise ValueError(f"Unknown item element: {name}")
                item_elements.append(child)
        for element in item_elements:
            if element.attrib:
                raise ValueError("Item definitions cannot contain XML attributes.")
            type_name = _local_name(element.tag)
            target_cls = ITEM_TYPE_REGISTRY[type_name]
            if target_cls._is_abstract:
                raise ValueError(f"Abstract item type cannot be instantiated: {type_name}")
            reference = _item_reference_from_element(element, type_name)
            item = context.resolve_reference(reference, type_name)
            _, key, _ = context._reference_info(reference, type_name)
            if key in context.defined_keys:
                raise ValueError(f"Duplicate full item definition: {type_name} {key[1]!r}")
            context.defined_keys.add(key)
            item._cogs_is_defined = True
            context.items_by_key[key] = item
        container = cls()
        for element in item_elements:
            type_name = _local_name(element.tag)
            reference = _item_reference_from_element(element, type_name)
            item = context.resolve_reference(reference, type_name)
            item._populate_from_element(element, context)
            container.items.append(item)
        for element in top_level:
            container.top_level_references.append(
                context.resolve_reference(_reference_dict_from_element(element))
            )
        return container

    def to_xml(self, *, xml_declaration: bool = False) -> str:
        return ET.tostring(
            self.to_element(),
            encoding="utf-8",
            xml_declaration=xml_declaration,
            short_empty_elements=True,
        ).decode("utf-8")

    @classmethod
    def from_xml(cls, value: str | bytes | bytearray) -> ItemContainer:
        root, namespaces, element_namespaces = _parse_xml(value)
        return cls.from_element(
            root,
            namespaces=namespaces,
            _element_namespaces=element_namespaces,
        )

    @classmethod
    def load_xml(
        cls,
        source: str | os.PathLike[str] | IO[str] | IO[bytes],
    ) -> ItemContainer:
        if hasattr(source, "read"):
            return cls.from_xml(source.read())
        return cls.from_xml(Path(source).read_bytes())

    def dump_xml(
        self,
        target: str | os.PathLike[str] | IO[str] | IO[bytes],
        *,
        xml_declaration: bool = True,
    ) -> None:
        value = ET.tostring(
            self.to_element(),
            encoding="utf-8",
            xml_declaration=xml_declaration,
            short_empty_elements=True,
        )
        if hasattr(target, "write"):
            try:
                target.write(value)
            except TypeError:
                target.write(value.decode("utf-8"))
            return
        Path(target).write_bytes(value)


# Registries and generated classes are appended below by COGS.
