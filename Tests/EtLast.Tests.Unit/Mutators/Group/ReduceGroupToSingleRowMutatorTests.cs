﻿namespace FizzCode.EtLast.Tests.Unit.Mutators.Aggregation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.LightWeight.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ReduceGroupToSingleRowMutatorTests
    {
        [TestMethod]
        public void ThrowsInvalidProcessParameterException()
        {
            Assert.That.ThrowsInvalidProcessParameterException<ReduceGroupToSingleRowMutator>();
        }

        [TestMethod]
        public void PartialAndFullRemoval()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.Person(topic),
                Mutators = new MutatorList()
                {
                    new InPlaceConvertMutator(topic, null)
                    {
                        Columns = new[] { "age" },
                        TypeConverter = new DecimalConverter(),
                    },
                    new ReduceGroupToSingleRowMutator(topic, null)
                    {
                        KeyColumns = new[] { "name" },
                        Selector = (proc, groupRows) =>
                        {
                            return groupRows
                                .Where(x => x.HasValue("age"))
                                .OrderBy(x => x.GetAs<decimal>("age"))
                                .FirstOrDefault();
                        }
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(5, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "A", ["age"] = 11m, ["height"] = 140, ["birthDate"] = new DateTime(2013, 5, 15, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2018, 1, 1, 0, 0, 0, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["age"] = 8m, ["height"] = 190, ["countryId"] = 1, ["birthDate"] = new DateTime(2011, 2, 1, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 13, 2, 0, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["age"] = 27m, ["height"] = 170, ["eyeColor"] = "green", ["countryId"] = 2, ["birthDate"] = new DateTime(2014, 1, 21, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 11, 21, 17, 11, 58, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["age"] = 39m, ["height"] = 160, ["eyeColor"] = "fake", ["birthDate"] = "2018.07.11", ["lastChangedTime"] = new DateTime(2017, 8, 1, 4, 9, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["age"] = -3m, ["height"] = 160, ["countryId"] = 1, ["lastChangedTime"] = new DateTime(2019, 1, 1, 23, 59, 59, 0) } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void IgnoreSelectorForSingleRowGroupsTrue()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.Person(topic),
                Mutators = new MutatorList()
                {
                    new InPlaceConvertMutator(topic, null)
                    {
                        Columns = new[] { "age" },
                        TypeConverter = new DecimalConverter(),
                    },
                    new ReduceGroupToSingleRowMutator(topic, null)
                    {
                        IgnoreSelectorForSingleRowGroups = true,
                        KeyColumns = new[] { "name" },
                        Selector = (proc, groupRows) =>
                        {
                            return groupRows
                                .Where(x => x.HasValue("age"))
                                .OrderBy(x => x.GetAs<decimal>("age"))
                                .FirstOrDefault();
                        },
                    },
                },
            };

            // note: this includes "fake" because Selector is not applied over single-row groups

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(6, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "A", ["age"] = 11m, ["height"] = 140, ["birthDate"] = new DateTime(2013, 5, 15, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2018, 1, 1, 0, 0, 0, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["age"] = 8m, ["height"] = 190, ["countryId"] = 1, ["birthDate"] = new DateTime(2011, 2, 1, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 13, 2, 0, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["age"] = 27m, ["height"] = 170, ["eyeColor"] = "green", ["countryId"] = 2, ["birthDate"] = new DateTime(2014, 1, 21, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 11, 21, 17, 11, 58, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["age"] = 39m, ["height"] = 160, ["eyeColor"] = "fake", ["birthDate"] = "2018.07.11", ["lastChangedTime"] = new DateTime(2017, 8, 1, 4, 9, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["age"] = -3m, ["height"] = 160, ["countryId"] = 1, ["lastChangedTime"] = new DateTime(2019, 1, 1, 23, 59, 59, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 6, ["name"] = "fake", ["height"] = 140, ["countryId"] = 5, ["birthDate"] = new DateTime(2018, 1, 9, 0, 0, 0, 0) } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void IgnoreSelectorForSingleRowGroupsDefaultFalse()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.Person(topic),
                Mutators = new MutatorList()
                {
                    new InPlaceConvertMutator(topic, null)
                    {
                        Columns = new[] { "age" },
                        TypeConverter = new DecimalConverter(),
                    },
                    new ReduceGroupToSingleRowMutator(topic, null)
                    {
                        KeyColumns = new[] { "name" },
                        Selector = (proc, groupRows) =>
                        {
                            if (groupRows.Count < 2)
                                throw new EtlException("wrong");

                            return groupRows[0];
                        },
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(1, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["name"] = "A", ["age"] = 17m, ["height"] = 160, ["eyeColor"] = "brown", ["countryId"] = 1, ["birthDate"] = new DateTime(2010, 12, 9, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 12, 0, 1, 0) } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(1, exceptions.Count);
            Assert.IsTrue(exceptions[0] is EtlException);
        }
    }
}