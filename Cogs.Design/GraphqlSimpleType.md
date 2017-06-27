# Graphql Type 

## Supported Simple Types

## String 
1. Represent as `String`
2. no specific way to define the Facets 

## boolean 
1. Represent as `Boolean`

## decimal 
1. Represent as `Float`
2. no specific way to define the Facets 

## float 
1. Represent as `Float`
2. no specific way to define the Facets 

## double 
1. Represent as `Float`
2. no specific way to define the Facets 

## duration
1. Define as below
    ```
    type duration {
        years : Int
        months : Int
        days: Int
        hour: Int
        minutes: Int
        seconds: Int
        timezone: String
    }
    ```
## datetime 
1. define as below
    ```
    type datetime {
        date: date
        time: time
        timezone: String
    }
    ```

## time 
1. define as below
    ```
    type time {
        hour: Int
        minutes: Int
        second: Int
        timezone: String
    }
    ```

## date
1. define as below
    ```
    type date {
        year: Int
        month: Int
        day: Int
        timezone: String
    }
    ```

## gYearMonth
1. define as below
    ```
    type gYearMonth {
        Year: Int
        Month: Int
        timezone: String
    }
    ```

## gYear
1. define as below 
    ```
    type gYear {
        Year: Int
        timezone: String
    }
    ```

## gMonthDay
1. define as below 
    ```
    type gMonthDay {
        Month: Int
        Day: Int
        timezone: String
    }
    ```

## gDay
1. define as below 
    ```
    type gDay {
        Day: Int
        timezone: String
    }
    ```

## gMonth
1. define as below 
    ```
    type gMonth {
        Month: Int
        timezone: String
    }
    ```

## anyURI
1. No specific to define URI
2. Graphql will automatically find the object type that you define within the schema

## Supported Derived Types

## language
1. 

## integer
1. Represent as `Int` 
2. no specific way to define the Facets 

## nonPositiveInteger
1. Represent as `Int`
2. no specific way to define the Facets 

## negativeInteger
1. Represent as `Int`
2. no specific way to define the Facets 

## long 
1. It is not define in graphql schema for now

## int
1. Represent as `Int`
2. no specific way to define the Facets 

## nonNegativeInteger	
1. Represent as `Int`
2. no specific way to define the Facets 

## unsignedLong	
1. Represent as `Int`
2. no specific way to define the Facets 

## positiveInteger	
1. Represent as `Int`
2. no specific way to define the Facets 

## cogsDate	
1. Define as below
    ```
    type cogsDate {
        dateTime : datetime
        date : date
        gYearMonth : gYearMonth
        gYear : gYear 
        duration : duration
    }
    ```