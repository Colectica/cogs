from __future__ import annotations

import json
import math
import os
import re
from dataclasses import dataclass, field, fields
from datetime import date, datetime, time, timedelta
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, ClassVar, IO
from xml.etree import ElementTree as ET

TARGET_NAMESPACE = __TARGET_NAMESPACE__
NAMESPACE_PREFIX = __NAMESPACE_PREFIX__
IDENTIFICATION_FIELDS = __IDENTIFICATION_FIELDS__
XML_NAMESPACE = "http://www.w3.org/XML/1998/namespace"
XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance"

ET.register_namespace("", TARGET_NAMESPACE)
ET.register_namespace("xsi", XSI_NAMESPACE)


def _q(name: str) -> str:
    return f"{{{TARGET_NAMESPACE}}}{name}"


def _local_name(name: str) -> str:
    return name.rsplit("}", 1)[-1]


def _json_dump_value(value: Any, indent: int | None = None, level: int = 0) -> str:
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, int):
        return str(value)
    if isinstance(value, Decimal):
        if not value.is_finite():
            raise ValueError("JSON numbers must be finite.")
        return str(value)
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
            pairs.append(json.dumps(key, ensure_ascii=False) + separator + _json_dump_value(item, indent, level + 1))
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
        parse_float=Decimal,
        parse_constant=_reject_json_constant,
        object_pairs_hook=_json_object,
    )


def _duration_to_milliseconds(value: timedelta) -> int | Decimal:
    microseconds = ((value.days * 86400 + value.seconds) * 1_000_000) + value.microseconds
    milliseconds = Decimal(microseconds) / Decimal(1000)
    return int(milliseconds) if milliseconds == milliseconds.to_integral_value() else milliseconds


def _duration_from_milliseconds(value: Any) -> timedelta:
    try:
        microseconds = Decimal(str(value)) * Decimal(1000)
    except InvalidOperation as exc:
        raise ValueError(f"Invalid millisecond duration: {value!r}") from exc
    if microseconds != microseconds.to_integral_value():
        raise ValueError("Durations more precise than one microsecond are not supported.")
    return timedelta(microseconds=int(microseconds))


def _duration_to_xml(value: timedelta) -> str:
    microseconds = ((value.days * 86400 + value.seconds) * 1_000_000) + value.microseconds
    sign = "-" if microseconds < 0 else ""
    microseconds = abs(microseconds)
    day_us = 86_400_000_000
    hour_us = 3_600_000_000
    minute_us = 60_000_000
    days, microseconds = divmod(microseconds, day_us)
    hours, microseconds = divmod(microseconds, hour_us)
    minutes, microseconds = divmod(microseconds, minute_us)
    seconds, fraction = divmod(microseconds, 1_000_000)
    second_text = str(seconds)
    if fraction:
        second_text += "." + f"{fraction:06d}".rstrip("0")
    return f"{sign}P{days}DT{hours}H{minutes}M{second_text}S"


_DURATION_PATTERN = re.compile(
    r"^(?P<sign>-)?P(?P<days>\d+)D(?:T(?P<hours>\d+)H(?P<minutes>\d+)M(?P<seconds>\d+(?:\.\d+)?)S)?$"
)


def _duration_from_xml(value: str) -> timedelta:
    match = _DURATION_PATTERN.fullmatch(value)
    if match is None:
        raise ValueError(
            "Only day/time XML durations emitted by COGS can be represented losslessly."
        )
    seconds = Decimal(match.group("seconds") or "0")
    microseconds = seconds * Decimal(1_000_000)
    if microseconds != microseconds.to_integral_value():
        raise ValueError("Durations more precise than one microsecond are not supported.")
    result = timedelta(
        days=int(match.group("days")),
        hours=int(match.group("hours") or "0"),
        minutes=int(match.group("minutes") or "0"),
        microseconds=int(microseconds),
    )
    return -result if match.group("sign") else result


def _parse_datetime(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def _parse_time(value: str) -> time:
    return time.fromisoformat(value.replace("Z", "+00:00"))


def _format_temporal(value: datetime | time) -> str:
    raw = value.isoformat()
    raw = re.sub(r"(\.\d*?[1-9])0+(?=(?:Z|[+-]\d{2}:\d{2})?$)", r"\1", raw)
    return re.sub(r"\.0+(?=(?:Z|[+-]\d{2}:\d{2})?$)", "", raw)


def _format_float(value: float) -> str:
    if math.isnan(value):
        return "NaN"
    if value == math.inf:
        return "INF"
    if value == -math.inf:
        return "-INF"
    return repr(value)


def _parse_float(value: str) -> float:
    if value == "INF":
        return math.inf
    if value == "-INF":
        return -math.inf
    if value == "NaN":
        return math.nan
    return float(value)


@dataclass(frozen=True)
class LangString:
    language: str
    value: str

    def to_json_value(self) -> dict[str, str]:
        return {"@language": self.language, "@value": self.value}

    @classmethod
    def from_json_value(cls, raw: Any) -> LangString:
        if not isinstance(raw, dict) or set(raw) != {"@language", "@value"}:
            raise ValueError("langString must contain exactly @language and @value.")
        return cls(language=str(raw["@language"]), value=str(raw["@value"]))


def _validate_timezone(value: str | None) -> None:
    if value is None:
        return
    if re.fullmatch(r"Z|[+-](?:0\d|1\d|2[0-3]):[0-5]\d", value) is None:
        raise ValueError(f"Invalid XML Schema timezone: {value!r}")


def _format_year(value: int) -> str:
    sign = "-" if value < 0 else ""
    return sign + f"{abs(value):04d}"


@dataclass(frozen=True)
class GYearMonth:
    year: int
    month: int
    timezone: str | None = None

    def __post_init__(self) -> None:
        if not 1 <= self.month <= 12:
            raise ValueError("month must be between 1 and 12")
        _validate_timezone(self.timezone)

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Year": self.year, "Month": self.month}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GYearMonth:
        _validate_gregorian_object(raw, {"Year", "Month"})
        return cls(int(raw["Year"]), int(raw["Month"]), raw.get("Timezone"))

    def to_xml_text(self) -> str:
        return f"{_format_year(self.year)}-{self.month:02d}{self.timezone or ''}"

    @classmethod
    def from_xml_text(cls, raw: str) -> GYearMonth:
        match = re.fullmatch(r"(-?\d{4,})-(\d{2})(Z|[+-]\d{2}:\d{2})?", raw)
        if match is None:
            raise ValueError(f"Invalid gYearMonth: {raw!r}")
        return cls(int(match.group(1)), int(match.group(2)), match.group(3))


@dataclass(frozen=True)
class GYear:
    year: int
    timezone: str | None = None

    def __post_init__(self) -> None:
        _validate_timezone(self.timezone)

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Year": self.year}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GYear:
        _validate_gregorian_object(raw, {"Year"})
        return cls(int(raw["Year"]), raw.get("Timezone"))

    def to_xml_text(self) -> str:
        return f"{_format_year(self.year)}{self.timezone or ''}"

    @classmethod
    def from_xml_text(cls, raw: str) -> GYear:
        match = re.fullmatch(r"(-?\d{4,})(Z|[+-]\d{2}:\d{2})?", raw)
        if match is None:
            raise ValueError(f"Invalid gYear: {raw!r}")
        return cls(int(match.group(1)), match.group(2))


@dataclass(frozen=True)
class GMonthDay:
    month: int
    day: int
    timezone: str | None = None

    def __post_init__(self) -> None:
        if not 1 <= self.month <= 12 or not 1 <= self.day <= 31:
            raise ValueError("month/day are outside their allowed ranges")
        _validate_timezone(self.timezone)

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Month": self.month, "Day": self.day}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GMonthDay:
        _validate_gregorian_object(raw, {"Month", "Day"})
        return cls(int(raw["Month"]), int(raw["Day"]), raw.get("Timezone"))

    def to_xml_text(self) -> str:
        return f"--{self.month:02d}-{self.day:02d}{self.timezone or ''}"

    @classmethod
    def from_xml_text(cls, raw: str) -> GMonthDay:
        match = re.fullmatch(r"--(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?", raw)
        if match is None:
            raise ValueError(f"Invalid gMonthDay: {raw!r}")
        return cls(int(match.group(1)), int(match.group(2)), match.group(3))


@dataclass(frozen=True)
class GMonth:
    month: int
    timezone: str | None = None

    def __post_init__(self) -> None:
        if not 1 <= self.month <= 12:
            raise ValueError("month must be between 1 and 12")
        _validate_timezone(self.timezone)

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Month": self.month}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GMonth:
        _validate_gregorian_object(raw, {"Month"})
        return cls(int(raw["Month"]), raw.get("Timezone"))

    def to_xml_text(self) -> str:
        return f"--{self.month:02d}{self.timezone or ''}"

    @classmethod
    def from_xml_text(cls, raw: str) -> GMonth:
        match = re.fullmatch(r"--(\d{2})(?:--)?(Z|[+-]\d{2}:\d{2})?", raw)
        if match is None:
            raise ValueError(f"Invalid gMonth: {raw!r}")
        return cls(int(match.group(1)), match.group(2))


@dataclass(frozen=True)
class GDay:
    day: int
    timezone: str | None = None

    def __post_init__(self) -> None:
        if not 1 <= self.day <= 31:
            raise ValueError("day must be between 1 and 31")
        _validate_timezone(self.timezone)

    def to_json_value(self) -> dict[str, Any]:
        result: dict[str, Any] = {"Day": self.day}
        if self.timezone is not None:
            result["Timezone"] = self.timezone
        return result

    @classmethod
    def from_json_value(cls, raw: Any) -> GDay:
        _validate_gregorian_object(raw, {"Day"})
        return cls(int(raw["Day"]), raw.get("Timezone"))

    def to_xml_text(self) -> str:
        return f"---{self.day:02d}{self.timezone or ''}"

    @classmethod
    def from_xml_text(cls, raw: str) -> GDay:
        match = re.fullmatch(r"---(\d{2})(Z|[+-]\d{2}:\d{2})?", raw)
        if match is None:
            raise ValueError(f"Invalid gDay: {raw!r}")
        return cls(int(match.group(1)), match.group(2))


def _validate_gregorian_object(raw: Any, required: set[str]) -> None:
    if not isinstance(raw, dict):
        raise TypeError("Gregorian values must be JSON objects.")
    allowed = required | {"Timezone"}
    if not required.issubset(raw) or not set(raw).issubset(allowed):
        raise ValueError(f"Expected {sorted(required)} and optional Timezone.")


@dataclass(frozen=True)
class CogsDate:
    value: datetime | date | GYearMonth | GYear | timedelta

    def to_json_value(self) -> dict[str, Any]:
        if isinstance(self.value, datetime):
            return {"DateTime": _format_temporal(self.value)}
        if isinstance(self.value, date):
            return {"Date": self.value.isoformat()}
        if isinstance(self.value, GYearMonth):
            return {"GYearMonth": self.value.to_json_value()}
        if isinstance(self.value, GYear):
            return {"GYear": self.value.to_json_value()}
        if isinstance(self.value, timedelta):
            return {"Duration": _duration_to_milliseconds(self.value)}
        raise TypeError(f"Unsupported cogsDate value: {type(self.value).__name__}")

    @classmethod
    def from_json_value(cls, raw: Any) -> CogsDate:
        if not isinstance(raw, dict) or len(raw) != 1:
            raise ValueError("cogsDate must contain exactly one active value.")
        name, value = next(iter(raw.items()))
        if name == "DateTime":
            return cls(_parse_datetime(value))
        if name == "Date":
            return cls(date.fromisoformat(value))
        if name == "GYearMonth":
            return cls(GYearMonth.from_json_value(value))
        if name == "GYear":
            return cls(GYear.from_json_value(value))
        if name == "Duration":
            return cls(_duration_from_milliseconds(value))
        raise ValueError(f"Unknown cogsDate member: {name}")

    def to_xml_text(self) -> str:
        if isinstance(self.value, datetime):
            return _format_temporal(self.value)
        if isinstance(self.value, date):
            return self.value.isoformat()
        if isinstance(self.value, (GYearMonth, GYear)):
            return self.value.to_xml_text()
        if isinstance(self.value, timedelta):
            return _duration_to_xml(self.value)
        raise TypeError(f"Unsupported cogsDate value: {type(self.value).__name__}")

    @classmethod
    def from_xml_text(cls, raw: str) -> CogsDate:
        if raw.startswith("P") or raw.startswith("-P"):
            return cls(_duration_from_xml(raw))
        if "T" in raw:
            return cls(_parse_datetime(raw))
        if re.fullmatch(r"-?\d{4,}-\d{2}(?:Z|[+-]\d{2}:\d{2})?", raw):
            return cls(GYearMonth.from_xml_text(raw))
        if re.fullmatch(r"-?\d{4,}(?:Z|[+-]\d{2}:\d{2})?", raw):
            return cls(GYear.from_xml_text(raw))
        return cls(date.fromisoformat(raw))


_STRING_TYPES = {"string", "language", "anyuri"}
_INTEGER_TYPES = {
    "nonpositiveinteger", "negativeinteger", "long", "int",
    "nonnegativeinteger", "unsignedlong", "positiveinteger",
}
_FLOAT_TYPES = {"float", "double"}


def _serialize_simple_json(type_name: str, value: Any) -> Any:
    lowered = type_name.lower()
    if lowered in _STRING_TYPES or lowered in _INTEGER_TYPES or lowered in _FLOAT_TYPES or lowered == "boolean":
        return value
    if lowered == "decimal":
        return value if isinstance(value, Decimal) else Decimal(str(value))
    if lowered == "datetime":
        return _format_temporal(value)
    if lowered == "date":
        return value.isoformat()
    if lowered == "time":
        return _format_temporal(value)
    if lowered == "duration":
        return _duration_to_milliseconds(value)
    if lowered == "gyearmonth":
        return value.to_json_value()
    if lowered == "gyear":
        return value.to_json_value()
    if lowered == "gmonthday":
        return value.to_json_value()
    if lowered == "gmonth":
        return value.to_json_value()
    if lowered == "gday":
        return value.to_json_value()
    if lowered == "langstring":
        return value.to_json_value()
    if lowered == "cogsdate":
        return value.to_json_value()
    raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _deserialize_simple_json(type_name: str, raw: Any) -> Any:
    lowered = type_name.lower()
    if lowered in _STRING_TYPES:
        if not isinstance(raw, str):
            raise TypeError(f"{type_name} must be a string.")
        return raw
    if lowered in _INTEGER_TYPES:
        if isinstance(raw, bool) or not isinstance(raw, int):
            raise TypeError(f"{type_name} must be an integer.")
        return raw
    if lowered in _FLOAT_TYPES:
        if isinstance(raw, bool) or not isinstance(raw, (int, float, Decimal)):
            raise TypeError(f"{type_name} must be a number.")
        return float(raw)
    if lowered == "decimal":
        if isinstance(raw, bool) or not isinstance(raw, (int, float, Decimal)):
            raise TypeError("decimal must be a number.")
        return Decimal(str(raw))
    if lowered == "boolean":
        if not isinstance(raw, bool):
            raise TypeError("boolean must be true or false.")
        return raw
    if lowered == "datetime":
        if not isinstance(raw, str):
            raise TypeError("dateTime must be a string.")
        return _parse_datetime(raw)
    if lowered == "date":
        if not isinstance(raw, str):
            raise TypeError("date must be a string.")
        return date.fromisoformat(raw)
    if lowered == "time":
        if not isinstance(raw, str):
            raise TypeError("time must be a string.")
        return _parse_time(raw)
    if lowered == "duration":
        return _duration_from_milliseconds(raw)
    if lowered == "gyearmonth":
        return GYearMonth.from_json_value(raw)
    if lowered == "gyear":
        return GYear.from_json_value(raw)
    if lowered == "gmonthday":
        return GMonthDay.from_json_value(raw)
    if lowered == "gmonth":
        return GMonth.from_json_value(raw)
    if lowered == "gday":
        return GDay.from_json_value(raw)
    if lowered == "langstring":
        return LangString.from_json_value(raw)
    if lowered == "cogsdate":
        return CogsDate.from_json_value(raw)
    raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _serialize_simple_xml(type_name: str, value: Any, element: ET.Element) -> None:
    lowered = type_name.lower()
    if lowered == "langstring":
        element.text = value.value
        element.set(f"{{{XML_NAMESPACE}}}lang", value.language)
        return
    if lowered == "boolean":
        element.text = "true" if value else "false"
    elif lowered in _STRING_TYPES or lowered in _INTEGER_TYPES or lowered == "decimal":
        element.text = str(value)
    elif lowered in _FLOAT_TYPES:
        element.text = _format_float(value)
    elif lowered in {"datetime", "date", "time"}:
        element.text = _format_temporal(value) if lowered != "date" else value.isoformat()
    elif lowered == "duration":
        element.text = _duration_to_xml(value)
    elif lowered in {"gyearmonth", "gyear", "gmonthday", "gmonth", "gday"}:
        element.text = value.to_xml_text()
    elif lowered == "cogsdate":
        element.text = value.to_xml_text()
    else:
        raise ValueError(f"Unsupported COGS primitive type: {type_name}")


def _deserialize_simple_xml(type_name: str, element: ET.Element) -> Any:
    lowered = type_name.lower()
    raw = element.text or ""
    if lowered == "langstring":
        unknown_attributes = set(element.attrib) - {f"{{{XML_NAMESPACE}}}lang"}
        if unknown_attributes:
            raise ValueError("langString contains unknown XML attributes.")
        language = element.get(f"{{{XML_NAMESPACE}}}lang")
        if language is None:
            raise ValueError("langString requires xml:lang.")
        return LangString(language=language, value=raw)
    if element.attrib:
        raise ValueError(f"{type_name} contains unknown XML attributes.")
    if lowered in _STRING_TYPES:
        return raw
    if lowered in _INTEGER_TYPES:
        return int(raw)
    if lowered == "decimal":
        return Decimal(raw)
    if lowered in _FLOAT_TYPES:
        return _parse_float(raw)
    if lowered == "boolean":
        if raw in {"true", "1"}:
            return True
        if raw in {"false", "0"}:
            return False
        raise ValueError(f"Invalid boolean: {raw!r}")
    if lowered == "datetime":
        return _parse_datetime(raw)
    if lowered == "date":
        return date.fromisoformat(raw)
    if lowered == "time":
        return _parse_time(raw)
    if lowered == "duration":
        return _duration_from_xml(raw)
    if lowered == "gyearmonth":
        return GYearMonth.from_xml_text(raw)
    if lowered == "gyear":
        return GYear.from_xml_text(raw)
    if lowered == "gmonthday":
        return GMonthDay.from_xml_text(raw)
    if lowered == "gmonth":
        return GMonth.from_xml_text(raw)
    if lowered == "gday":
        return GDay.from_xml_text(raw)
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


class _Context:
    def __init__(self) -> None:
        self.items_by_key: dict[str, CogsItem] = {}
        self.defined_keys: set[str] = set()

    def _make_key(self, type_name: str, raw: dict[str, Any]) -> str:
        missing = [wire for wire, _ in IDENTIFICATION_FIELDS if wire not in raw]
        if missing:
            raise ValueError(f"Reference is missing identification fields: {', '.join(missing)}")
        values = [raw[wire] for wire, _ in IDENTIFICATION_FIELDS]
        return type_name + "|" + _json_dump_value(values)

    def resolve_reference(
        self,
        raw: Any,
        expected_type: str | None = None,
        allow_subtypes: bool = True,
    ) -> CogsItem:
        if not isinstance(raw, dict):
            raise TypeError("Item references must be objects.")
        allowed = {"$type"} | {wire for wire, _ in IDENTIFICATION_FIELDS}
        unknown = set(raw) - allowed
        if unknown:
            raise ValueError(f"Unknown reference fields: {', '.join(sorted(unknown))}")
        type_name = raw.get("$type")
        if not isinstance(type_name, str):
            raise ValueError("Item references require $type.")
        try:
            actual_cls = ITEM_TYPE_REGISTRY[type_name]
        except KeyError as exc:
            raise ValueError(f"Unknown item type: {type_name}") from exc
        if actual_cls._is_abstract:
            raise ValueError(f"Abstract item type cannot be instantiated: {type_name}")
        if expected_type is not None:
            expected_cls = ITEM_TYPE_REGISTRY[expected_type]
            if not issubclass(actual_cls, expected_cls):
                raise TypeError(f"{type_name} is not assignable to {expected_type}.")
        key = self._make_key(type_name, raw)
        item = self.items_by_key.get(key)
        if item is None:
            item = actual_cls()
            by_wire = _field_by_wire_name(actual_cls)
            for wire_name, attribute_name in IDENTIFICATION_FIELDS:
                metadata = by_wire[wire_name].metadata
                setattr(item, attribute_name, _deserialize_simple_json(metadata["type_name"], raw[wire_name]))
            self.items_by_key[key] = item
        return item

    def load_item(self, raw: Any) -> CogsItem:
        if not isinstance(raw, dict) or not isinstance(raw.get("$type"), str):
            raise ValueError("Serialized items require a $type discriminator.")
        type_name = raw["$type"]
        reference = {"$type": type_name}
        for wire_name, _ in IDENTIFICATION_FIELDS:
            if wire_name in raw:
                reference[wire_name] = raw[wire_name]
        item = self.resolve_reference(reference, type_name, True)
        key = self._make_key(type_name, raw)
        if key in self.defined_keys:
            raise ValueError(f"Duplicate full item definition: {type_name}")
        self.defined_keys.add(key)
        item._populate_from_dict(raw, self)
        return item


@dataclass
class CogsValue:
    _cogs_type: ClassVar[str] = ""
    _is_item: ClassVar[bool] = False
    _is_abstract: ClassVar[bool] = False
    _emit_type_field: ClassVar[bool] = False

    def _to_dict_with_context(self, context: _Context) -> dict[str, Any]:
        if self._is_abstract:
            raise TypeError(f"Abstract type cannot be serialized: {self._cogs_type}")
        result: dict[str, Any] = {}
        if self._emit_type_field:
            result["$type"] = self._cogs_type
        for item in fields(self):
            value = getattr(self, item.name)
            if value is None or (item.metadata["many"] and not value):
                continue
            raw = _serialize_field_json(value, item.metadata, context)
            result[item.metadata["cogs_name"]] = raw
        return result

    def to_dict(self) -> dict[str, Any]:
        return self._to_dict_with_context(_Context())

    def to_json(self, *, indent: int | None = None) -> str:
        if indent is not None and indent < 0:
            raise ValueError("indent cannot be negative")
        return _json_dump_value(self.to_dict(), indent)

    def _populate_from_dict(self, data: Any, context: _Context) -> None:
        if not isinstance(data, dict):
            raise TypeError(f"{self._cogs_type} must be a JSON object.")
        by_wire = _field_by_wire_name(type(self))
        allowed = set(by_wire)
        if self._is_item or self._emit_type_field:
            allowed.add("$type")
        unknown = set(data) - allowed
        if unknown:
            raise ValueError(f"Unknown fields for {self._cogs_type}: {', '.join(sorted(unknown))}")
        if "$type" in data and data["$type"] != self._cogs_type:
            raise ValueError(f"Expected $type {self._cogs_type}, got {data['$type']!r}.")
        for wire_name, item in by_wire.items():
            if wire_name not in data:
                continue
            setattr(self, item.name, _deserialize_field_json(data[wire_name], item.metadata, context))

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> CogsValue:
        context = _Context()
        if issubclass(cls, CogsItem):
            result = context.load_item(data)
            if not isinstance(result, cls):
                raise TypeError(f"{type(result).__name__} is not assignable to {cls.__name__}.")
            return result
        target_cls = cls
        if "$type" in data:
            if not isinstance(data["$type"], str):
                raise TypeError("$type must be a string.")
            candidate = _type_for_name(data["$type"])
            if not issubclass(candidate, cls) or candidate._is_abstract:
                raise TypeError(f"{candidate.__name__} is not assignable to {cls.__name__}.")
            target_cls = candidate
        if target_cls._is_abstract:
            raise ValueError(f"Abstract type {target_cls.__name__} requires $type.")
        instance = target_cls()
        instance._populate_from_dict(data, context)
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
        if declared_type and self._cogs_type != declared_type:
            declared_cls = _type_for_name(declared_type)
            if not allow_subtypes or not isinstance(self, declared_cls):
                raise TypeError(f"{self._cogs_type} is not allowed where {declared_type} is declared.")
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
        return self._to_element_with_context(element_name or self._cogs_type, _Context())

    def to_xml(
        self,
        element_name: str | None = None,
        *,
        xml_declaration: bool = False,
    ) -> str:
        return ET.tostring(
            self.to_element(element_name), encoding="unicode",
            xml_declaration=xml_declaration, short_empty_elements=True,
        )

    @classmethod
    def from_element(cls, element: ET.Element) -> CogsValue:
        context = _Context()
        target_cls = _target_class_from_element(cls, element, True)
        instance = target_cls()
        instance._populate_from_element(element, context)
        return instance

    @classmethod
    def from_xml(cls, value: str | bytes) -> CogsValue:
        return cls.from_element(ET.fromstring(value))

    def _populate_from_element(self, element: ET.Element, context: _Context) -> None:
        unknown_attributes = set(element.attrib) - {f"{{{XSI_NAMESPACE}}}type"}
        if unknown_attributes:
            raise ValueError(f"Unknown XML attributes for {self._cogs_type}.")
        if element.text and element.text.strip():
            raise ValueError(f"Unexpected XML text for {self._cogs_type}.")
        by_wire = _field_by_wire_name(type(self))
        grouped: dict[str, list[ET.Element]] = {}
        for child in element:
            if not child.tag.startswith("{" + TARGET_NAMESPACE + "}"):
                raise ValueError(f"Unexpected XML namespace on {_local_name(child.tag)}.")
            grouped.setdefault(_local_name(child.tag), []).append(child)
        unknown = set(grouped) - set(by_wire)
        if unknown:
            raise ValueError(f"Unknown XML elements for {self._cogs_type}: {', '.join(sorted(unknown))}")
        for wire_name, item in by_wire.items():
            matches = grouped.get(wire_name, [])
            if item.metadata["many"]:
                setattr(self, item.name, [_deserialize_field_xml(child, item.metadata, context) for child in matches])
            elif len(matches) > 1:
                raise ValueError(f"{wire_name} occurs more than once.")
            elif matches:
                setattr(self, item.name, _deserialize_field_xml(matches[0], item.metadata, context))


@dataclass
class CogsItem(CogsValue):
    _is_item: ClassVar[bool] = True

    def _to_dict_with_context(self, context: _Context) -> dict[str, Any]:
        result = {"$type": self._cogs_type}
        result.update(super()._to_dict_with_context(context))
        return result

    def to_reference_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {"$type": self._cogs_type}
        by_wire = _field_by_wire_name(type(self))
        for wire_name, attribute_name in IDENTIFICATION_FIELDS:
            item = by_wire[wire_name]
            value = getattr(self, attribute_name)
            if value is None:
                raise ValueError(f"Reference field {wire_name} is not set.")
            result[wire_name] = _serialize_field_json(value, item.metadata, _Context())
        return result


def _serialize_field_json(value: Any, metadata: Any, context: _Context) -> Any:
    if metadata["many"]:
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
        if not isinstance(value, expected):
            raise TypeError(f"Invalid item type for {metadata['cogs_name']}.")
        return value.to_reference_dict()
    expected = TYPE_REGISTRY[metadata["type_name"]]
    if not isinstance(value, expected) or (not metadata["allow_subtypes"] and type(value) is not expected):
        raise TypeError(f"Invalid object type for {metadata['cogs_name']}.")
    return value._to_dict_with_context(context)


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
        return context.resolve_reference(raw, metadata["type_name"], metadata["allow_subtypes"])
    if not isinstance(raw, dict):
        raise TypeError(f"{metadata['cogs_name']} must be an object.")
    target_cls = TYPE_REGISTRY[metadata["type_name"]]
    if "$type" in raw:
        if not isinstance(raw["$type"], str):
            raise TypeError("$type must be a string.")
        candidate = _type_for_name(raw["$type"])
        if not issubclass(candidate, target_cls) or candidate._is_abstract:
            raise TypeError(f"{candidate.__name__} is not assignable to {target_cls.__name__}.")
        if not metadata["allow_subtypes"] and candidate is not target_cls:
            raise TypeError(f"Subtypes are not allowed for {metadata['cogs_name']}.")
        target_cls = candidate
    if target_cls._is_abstract:
        raise ValueError(f"Abstract type {target_cls.__name__} requires $type.")
    instance = target_cls()
    instance._populate_from_dict(raw, context)
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
        if not isinstance(value, expected):
            raise TypeError(f"Invalid item type for {metadata['cogs_name']}.")
        element = ET.Element(_q(metadata["cogs_name"]))
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
    if not isinstance(value, CogsValue):
        raise TypeError(f"{metadata['cogs_name']} requires a COGS value.")
    return value._to_element_with_context(
        metadata["cogs_name"], context, metadata["type_name"], metadata["allow_subtypes"]
    )


def _deserialize_field_xml(element: ET.Element, metadata: Any, context: _Context) -> Any:
    kind = metadata["kind"]
    if kind == "simple":
        return _deserialize_simple_xml(metadata["type_name"], element)
    if kind == "item":
        raw = _reference_dict_from_element(element)
        return context.resolve_reference(raw, metadata["type_name"], metadata["allow_subtypes"])
    target_cls = _target_class_from_element(TYPE_REGISTRY[metadata["type_name"]], element, metadata["allow_subtypes"])
    instance = target_cls()
    instance._populate_from_element(element, context)
    return instance


def _target_class_from_element(
    declared_cls: type[CogsValue], element: ET.Element, allow_subtypes: bool
) -> type[CogsValue]:
    xsi_type = element.get(f"{{{XSI_NAMESPACE}}}type")
    if xsi_type is None:
        if declared_cls._is_abstract:
            raise ValueError(f"Abstract type {declared_cls.__name__} requires xsi:type.")
        return declared_cls
    if not allow_subtypes:
        raise ValueError(f"xsi:type is not allowed for {declared_cls.__name__}.")
    type_name = xsi_type.rsplit(":", 1)[-1]
    candidate = _type_for_name(type_name)
    if not issubclass(candidate, declared_cls) or candidate._is_abstract:
        raise TypeError(f"Invalid xsi:type {type_name} for {declared_cls.__name__}.")
    return candidate


def _reference_dict_from_element(element: ET.Element) -> dict[str, Any]:
    if element.attrib:
        raise ValueError("XML references cannot contain attributes.")
    if element.text and element.text.strip():
        raise ValueError("XML references cannot contain text content.")
    grouped: dict[str, ET.Element] = {}
    for child in element:
        if not child.tag.startswith("{" + TARGET_NAMESPACE + "}"):
            raise ValueError(f"Unexpected XML namespace on {_local_name(child.tag)}.")
        name = _local_name(child.tag)
        if name in grouped:
            raise ValueError(f"XML reference field {name} occurs more than once.")
        grouped[name] = child
    allowed = {"TypeOfObject"} | {wire for wire, _ in IDENTIFICATION_FIELDS}
    unknown = set(grouped) - allowed
    if unknown:
        raise ValueError(f"Unknown XML reference fields: {', '.join(sorted(unknown))}")
    if "TypeOfObject" not in grouped:
        raise ValueError("XML references require TypeOfObject.")
    type_name = grouped["TypeOfObject"].text or ""
    try:
        target_cls = ITEM_TYPE_REGISTRY[type_name]
    except KeyError as exc:
        raise ValueError(f"Unknown item type: {type_name}") from exc
    by_wire = _field_by_wire_name(target_cls)
    result: dict[str, Any] = {"$type": type_name}
    for wire_name, _ in IDENTIFICATION_FIELDS:
        if wire_name not in grouped:
            raise ValueError(f"XML reference is missing {wire_name}.")
        result[wire_name] = _deserialize_simple_xml(
            by_wire[wire_name].metadata["type_name"], grouped[wire_name]
        )
    return result


@dataclass
class ItemContainer:
    items: list[CogsItem] = field(default_factory=list)
    top_level_references: list[CogsItem] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        context = _Context()
        for item in self.items:
            reference = item.to_reference_dict()
            key = context._make_key(item._cogs_type, reference)
            if key in context.defined_keys:
                raise ValueError(f"Duplicate full item definition: {item._cogs_type}")
            context.defined_keys.add(key)
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
        context = _Context()
        container = cls()
        for raw in data["items"]:
            container.items.append(context.load_item(raw))
        references = data.get("topLevelReferences", [])
        if not isinstance(references, list):
            raise TypeError("topLevelReferences must be an array.")
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
    def load_json(cls, source: str | os.PathLike[str] | IO[str]) -> ItemContainer:
        if hasattr(source, "read"):
            return cls.from_json(source.read())
        with open(source, "r", encoding="utf-8") as handle:
            return cls.from_json(handle.read())

    def dump_json(
        self, target: str | os.PathLike[str] | IO[str], *, indent: int | None = 2
    ) -> None:
        value = self.to_json(indent=indent)
        if hasattr(target, "write"):
            target.write(value)
            return
        with open(target, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(value)

    def to_element(self) -> ET.Element:
        context = _Context()
        root = ET.Element(_q("ItemContainer"))
        root.set(f"xmlns:{NAMESPACE_PREFIX}", TARGET_NAMESPACE)
        for item in self.top_level_references:
            metadata = {
                "cogs_name": "TopLevelReference", "kind": "item", "type_name": item._cogs_type,
                "allow_subtypes": True, "many": True,
            }
            root.append(_serialize_field_xml(item, metadata, context))
        for item in self.items:
            reference = item.to_reference_dict()
            key = context._make_key(item._cogs_type, reference)
            if key in context.defined_keys:
                raise ValueError(f"Duplicate full item definition: {item._cogs_type}")
            context.defined_keys.add(key)
            root.append(item._to_element_with_context(item._cogs_type, context))
        return root

    @classmethod
    def from_element(cls, root: ET.Element) -> ItemContainer:
        if root.tag != _q("ItemContainer"):
            raise ValueError("Expected a namespace-qualified ItemContainer root element.")
        if root.attrib:
            raise ValueError("ItemContainer cannot contain XML attributes.")
        if root.text and root.text.strip():
            raise ValueError("ItemContainer cannot contain text content.")
        context = _Context()
        container = cls()
        top_level: list[ET.Element] = []
        item_elements: list[ET.Element] = []
        seen_items = False
        for child in root:
            if not child.tag.startswith("{" + TARGET_NAMESPACE + "}"):
                raise ValueError(f"Unexpected XML namespace on {_local_name(child.tag)}.")
            name = _local_name(child.tag)
            if name == "TopLevelReference":
                if seen_items:
                    raise ValueError("TopLevelReference elements must precede items.")
                top_level.append(child)
            else:
                seen_items = True
                item_elements.append(child)
        for element in item_elements:
            if element.attrib:
                raise ValueError("Item definitions cannot contain XML attributes.")
            type_name = _local_name(element.tag)
            if type_name not in ITEM_TYPE_REGISTRY:
                raise ValueError(f"Unknown item element: {type_name}")
            target_cls = ITEM_TYPE_REGISTRY[type_name]
            if target_cls._is_abstract:
                raise ValueError(f"Abstract item type cannot be instantiated: {type_name}")
            by_wire = _field_by_wire_name(target_cls)
            grouped: dict[str, list[ET.Element]] = {}
            for child in element:
                grouped.setdefault(_local_name(child.tag), []).append(child)
            raw_reference: dict[str, Any] = {"$type": type_name}
            for wire_name, _ in IDENTIFICATION_FIELDS:
                matches = grouped.get(wire_name, [])
                if len(matches) != 1:
                    raise ValueError(f"Item {type_name} requires one {wire_name}.")
                raw_reference[wire_name] = _deserialize_simple_xml(
                    by_wire[wire_name].metadata["type_name"], matches[0]
                )
            item = context.resolve_reference(raw_reference, type_name, True)
            key = context._make_key(type_name, raw_reference)
            if key in context.defined_keys:
                raise ValueError(f"Duplicate full item definition: {type_name}")
            context.defined_keys.add(key)
            item._populate_from_element(element, context)
            container.items.append(item)
        for element in top_level:
            container.top_level_references.append(context.resolve_reference(_reference_dict_from_element(element)))
        return container

    def to_xml(self, *, xml_declaration: bool = False) -> str:
        return ET.tostring(
            self.to_element(), encoding="unicode", xml_declaration=xml_declaration,
            short_empty_elements=True,
        )

    @classmethod
    def from_xml(cls, value: str | bytes) -> ItemContainer:
        return cls.from_element(ET.fromstring(value))

    @classmethod
    def load_xml(
        cls, source: str | os.PathLike[str] | IO[str] | IO[bytes]
    ) -> ItemContainer:
        if hasattr(source, "read"):
            return cls.from_xml(source.read())
        return cls.from_element(ET.parse(source).getroot())

    def dump_xml(
        self,
        target: str | os.PathLike[str] | IO[str] | IO[bytes],
        *,
        xml_declaration: bool = True,
    ) -> None:
        value = ET.tostring(
            self.to_element(), encoding="utf-8", xml_declaration=xml_declaration,
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
