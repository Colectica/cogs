using Cogs.Dto;
using Cogs.Model;
using Cogs.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Cogs.Tests
{
    public class ModelTests
    {
        [Fact]
        public void DuplicatePropertiesInSameDatatypeTest()
        {
            CogsDtoModel dto = new CogsDtoModel();
            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "TestItem"
            };
            Dto.Property property = new Dto.Property()
            {
                Name = "Duplicate",
                DataType = "string",
                MinCardinality = "0",
                MaxCardinality = "n"
            };
            item.Properties.Add(property);
            item.Properties.Add(property);
            dto.ItemTypes.Add(item);

            var errors = DtoValidation.CheckDuplicatePropertiesInSameItem(dto);

            Assert.NotEmpty(errors);

        }

        [Fact]
        public void ReusedPropertyNamesShouldHaveSameDatatype()
        {

            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "TestItem1"
            };
            Dto.ItemType item2 = new Dto.ItemType()
            {
                Name = "TestItem2"
            };
            dto.ItemTypes.Add(item);
            dto.ItemTypes.Add(item2);

            Dto.Property property = new Dto.Property()
            {
                Name = "Duplicate",
                DataType = "string",
                MinCardinality = "0",
                MaxCardinality = "n"
            };
            Dto.Property property2 = new Dto.Property()
            {
                Name = "Duplicate",
                DataType = "bool",
                MinCardinality = "0",
                MaxCardinality = "n"
            };
            item.Properties.Add(property);
            item2.Properties.Add(property2);


            var errors = DtoValidation.CheckReusedPropertyNamesShouldHaveSameDatatype(dto);

            Assert.NotEmpty(errors);
        }


        [Fact]
        public void DataTypesMustBeDefined()
        {

            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "TestItem1"
            };
            dto.ItemTypes.Add(item);

            Dto.Property property = new Dto.Property()
            {
                Name = "MyProp",
                DataType = "Unknown",
                MinCardinality = "0",
                MaxCardinality = "n"
            };
            item.Properties.Add(property);

            var errors = DtoValidation.CheckDataTypesMustBeDefined(dto);

            Assert.NotEmpty(errors);
        }

        [Fact]
        public void DataTypeNamesShouldMatchCase()
        {
            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "TestItem1"
            };
            dto.ItemTypes.Add(item);

            Dto.Property property = new Dto.Property()
            {
                Name = "MyProp",
                DataType = "StrinG",
                MinCardinality = "0",
                MaxCardinality = "n"
            };
            item.Properties.Add(property);

            var errors = DtoValidation.CheckDataTypeNamesShouldMatchCase(dto);

            Assert.NotEmpty(errors);
        }

        
        [Fact]
        public void DataTypeNamesShouldNotConflictWithBuiltins()
        {
            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "String"
            };
            dto.ItemTypes.Add(item);
            
            var errors = DtoValidation.CheckDataTypeNamesShouldNotConflictWithBuiltins(dto);

            Assert.NotEmpty(errors);
        }

        [Fact]
        public void DataTypeNamesShouldBePascalCase()
        {
            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "myNonPascalCaseItem"
            };
            dto.ItemTypes.Add(item);

            var errors = DtoValidation.CheckDataTypeNamesShouldBePascalCase(dto);

            Assert.NotEmpty(errors);
        }

        [Fact]
        public void PropertyNamesShouldBePascalCase()
        {
            CogsDtoModel dto = new CogsDtoModel();

            Dto.ItemType item = new Dto.ItemType()
            {
                Name = "TestItem1"
            };
            dto.ItemTypes.Add(item);

            Dto.Property property = new Dto.Property()
            {
                Name = "myProp",
            };
            item.Properties.Add(property);

            var errors = DtoValidation.CheckPropertyNamesShouldBePascalCase(dto);

            Assert.NotEmpty(errors);
        }
        
    }
}
