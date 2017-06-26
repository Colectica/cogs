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
    }
    ```
## datetime 
1. define as below
    ```
    type datetime {
        date: date
        time: time
    }
    ```

## time 
1. define as below
    ```
    type time {
        hour: Int
        minutes: Int
        second: Int
    }
    ```

## date
1. define as below
    ```
    type date {
        year: Int
        month: Int
        day: Int
    }
    ```

## gYearMonth
1. define as below
    ```
    type gYearMonth {
        Year: Int
        Month: Int
    }
    ```

## gYear
1. define as below 
    ```
    type gYear {
        Year: Int
    }
    ```

## gMonthDay
1. define as below 
    ```
    type gMonthDay {
        Month: Int
        Day: Int
    }
    ```

## gDay
1. define as below 
    ```
    type gDay {
        Day: Int
    }
    ```

## gMonth
1. define as below 
    ```
    type gMonth {
        Month: Int
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

## nonPositiveInteger
1. Represent as `Int`

## negativeInteger
1. Represent as `Int`

## long 
1. It is not define in graphql schema for now

## int
1. Represent as `Int`

## nonNegativeInteger	
1. Represent as `Int`

## unsignedLong	
1. Represent as `Int`

## positiveInteger	
1. Represent as `Int`

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