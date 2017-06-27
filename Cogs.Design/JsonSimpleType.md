# Json Simple Type


## Supported Simple Type

## String 
1. Represent as `string` 

2. Facets :

    - `maxlength` - represent as `maxLength` by giving it an integer value
    - `minlength` - represent as `minLength` by giving it an integer value
    - `enumeration` - represent as `enum` by giving a list of strings
    - `pattern` - represent as `pattern` by giving a regex expression


## Boolean

1. Represent as `boolean`

## Decimal

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## Float

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## Double

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## Duration 

1. Same format as XML Schema's dayTimeDuration

2. However, Json has another way to define duration which is in milliseconds. The value should be between a specified time and midnight, 00:00 of January 1, 1970 UTC

3. Represent as below
    ```
    "duration" : {
        "type" : "number",
        "format" : "utc-millisec"
    }
    ```

## DateTime 
1. Represent in the form of string, example
    ```
    "datetime" : "2015-09-22T10:30:06.000Z"
    ```
2. Json Schema is represent as below
    ```
    "datetime" : {
        "type" : "string",
        "format" : "date-time"
    }
    ```

## Time

1. Represent in the form of string, example:
    ```
    "time" : "10:30:06.000Z"
    ```

2. Json Schema is represent as below
    ```
    "time" : {
        "type" : "string",
        "format" : "time"
    }
    ```

## Date

1. Represent in the form of string, example: 
    ```
    "date" : "1982-01-15"
    ```

2. Json Schema is represented as below
    ```
    "date" :  {
        "type" : "string",
        "format" : "date"
    }
    ```

## GYearMonth

1. Represent as below
    ```
    "GYearMonth" : {
        "type" : "object",
        "properties" : {
            "year" : {"type" : "integer"},
            "month" : {"type" : "integer"},
            "timezone" : {"type" : "string"}
        }
        "required" : ["year", "month"]
    }
    ```
## GYear

1. Represent as below
    ```
    "year" : {
        "type" : "object",
        "properties" : {
            "year" : {"type" : "integer"},
            "timezone" : {"type" : "string"} 
        }
    }
    ```
## GMonthDay

1. Represent as below
    ```
    "GMonthDay" : {
        "type" : "object",
        "properties" : {
            "month" : {"type" : "integer"},
            "day" : {"type" : "integer"},
            "timezone" : {"type" : "string"}
        }
        "required" : ["month", "day"]
    }
    ```

## GDay

1. Represent as below
    ```
    "day" : {
        "type" : "object",
        "properties" : {
            "day" : {"type" : "integer"},
           "timezone" : {"type" : "string"} 
        }
    }
    ```

## GMonth

1. Represent as below
    ```
    "month" : {
        "type" : "object",
        "properties" : {
            "month" : {"type" : "integer"},
            "timezone" : {"type" : "string"} 
        }
    }
    ```

## AnyURI

1. Json Schema is represented as below
    ```
    "anyuri" :  {
        "type" : "string",
        "format" : "uri"
    }
    ```


## Supported Derived Types

## Language

1. Represent as below
    ```
    "Language" {
        "type" : "string"
    }
    ```

## Integer

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## nonPositiveInteger

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## negativeInteger

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## Long 

1. Represent as `number`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)
3. Schema as below 
    ```
        "type" : "number",
        "minInclusive" : -9223372036854775808,
        "maxInclusive" : 9223372036854775807,
        "minExclusive" : true,
        "maxExclusive" : true
    ```

## Int 

1. Represent as `integer`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

3. Schema as below 
    ```
        "type" : "number",
        "minInclusive" : -2147483648,
        "maxInclusive" : 2147483647,
        "minExclusive" : true,
        "maxExclusive" : true
    ```

## nonNegativeInteger

1. Represent as `integer`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

## unsignedLong

1. Represent as `integer`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

3. Schema as below 
    ```
        "type" : "number",
        "minInclusive" : 0,
        "maxInclusive" : 18446744073709551615,
        "minExclusive" : true,
        "maxExclusive" : true
    ```

## positiveInteger

1. Represent as `integer`

2. Facets:
    - `minInclusive` - represent as `minimum` (minimum value it can be)
    - `maxInclusive` - represent as `maximum` (maximum value it can be)
    - `minExclusive` - represent as `exclusiveMinimum` (boolean type - do we include the minInclusive or not)
    - `maxExclusive` - represent as `exclusiveMaximum` (boolean type - do we include the maxInclusive or not)

3. Schema as below 
    ```
        "type" : "number",
        "minInclusive" : 0,
        "minExclusive" : true,
    ```


## cogsDate

1. Represent as below
    ```
    "cogsdate" : {
        "type: "object",
        "properties" : {
            "dateTime" : { $ref : #/path/datetime },
            "date" : { $ref : #/path/date },
            "gYearMonth" : {$ref : #/path/GYearMonth},
            "gYear" : {$ref : #/path/gYear},
            "duration" : {$ref : #/path/duration}
        }
    }
    ```