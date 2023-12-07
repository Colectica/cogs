# generates LinkML from cogs model

spec: https://linkml.io/linkml/

## TODO

* check output and handling of properies
* handle IdentifiableType:
  ```yaml
  IdentifiableType:
    unique_keys:
      main:
        unique_key_slots:
          - URN
          - Agency
          - ID
          - Version
  ```
* check if prefix should be added as a argument
* trigger LinkML output
