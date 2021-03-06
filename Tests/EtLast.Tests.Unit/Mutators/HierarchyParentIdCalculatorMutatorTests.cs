﻿namespace FizzCode.EtLast.Tests.Unit.Mutators
{
    using System.Collections.Generic;
    using FizzCode.LightWeight.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HierarchyParentIdCalculatorMutatorTests
    {
        protected string[] SampleColumns { get; } = { "id", "name", "level1", "level2", "level3" };

        protected object[][] SampleRows { get; } = {
                new object[] { 0, "A", "AAA" },
                new object[] { 1, "B", null, "BBB" },
                new object[] { 2, "C", null, null, "CCC" },
                new object[] { 3, "D", null, null, "DDD" },
                new object[] { 4, "E", null, "EEE" },
                new object[] { 5, "F", null, "FFF" },
        };

        [TestMethod]
        public void KeepOriginalLevelColumns()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.RoleHierarchy(topic),
                Mutators = new MutatorList()
                {
                    new HierarchyParentIdCalculatorMutator(topic, null)
                    {
                        IdentityColumn = "id",
                        NewColumnWithParentId = "parentId",
                        NewColumnWithLevel = "level",
                        LevelColumns = new[] { "level1", "level2", "level3" },
                        RemoveLevelColumns = false,
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(6, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["name"] = "A", ["level1"] = "AAA", ["level"] = 0 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["level2"] = "BBB", ["parentId"] = 0, ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["level3"] = "CCC", ["parentId"] = 1, ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["level3"] = "DDD", ["parentId"] = 1, ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["level2"] = "EEE", ["parentId"] = 0, ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "F", ["level2"] = "FFF", ["parentId"] = 0, ["level"] = 1 } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void RemoveLevelColumns()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.RoleHierarchy(topic),
                Mutators = new MutatorList()
                {
                    new HierarchyParentIdCalculatorMutator(topic, null)
                    {
                        IdentityColumn = "id",
                        NewColumnWithParentId = "parentId",
                        NewColumnWithLevel = "level",
                        LevelColumns = new[] { "level1", "level2", "level3" },
                        RemoveLevelColumns = true,
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(6, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["name"] = "A", ["level"] = 0 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["parentId"] = 0, ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["parentId"] = 1, ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["parentId"] = 1, ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["parentId"] = 0, ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "F", ["parentId"] = 0, ["level"] = 1 } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void NoNewLevelColumn()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.RoleHierarchy(topic),
                Mutators = new MutatorList()
                {
                    new HierarchyParentIdCalculatorMutator(topic, null)
                    {
                        IdentityColumn = "id",
                        NewColumnWithParentId = "parentId",
                        LevelColumns = new[] { "level1", "level2", "level3" },
                        RemoveLevelColumns = true,
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(6, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["name"] = "A" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["parentId"] = 0 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["parentId"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["parentId"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["parentId"] = 0 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "F", ["parentId"] = 0 } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void IdentityColumnIsString()
        {
            var topic = TestExecuter.GetTopic();
            var builder = new ProcessBuilder()
            {
                InputProcess = TestData.RoleHierarchy(topic),
                Mutators = new MutatorList()
                {
                    new InPlaceConvertMutator(topic, "ConvertIdToString")
                    {
                        Columns = new[] {"id" },
                        TypeConverter = new StringConverter(),
                    },
                    new HierarchyParentIdCalculatorMutator(topic, null)
                    {
                        IdentityColumn = "id",
                        NewColumnWithParentId = "parentId",
                        NewColumnWithLevel = "level",
                        LevelColumns = new[] { "level1", "level2", "level3" },
                        RemoveLevelColumns = false,
                    },
                },
            };

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(6, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "0", ["name"] = "A", ["level1"] = "AAA", ["level"] = 0 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "1", ["name"] = "B", ["level2"] = "BBB", ["parentId"] = "0", ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "2", ["name"] = "C", ["level3"] = "CCC", ["parentId"] = "1", ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "3", ["name"] = "D", ["level3"] = "DDD", ["parentId"] = "1", ["level"] = 2 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "4", ["name"] = "E", ["level2"] = "EEE", ["parentId"] = "0", ["level"] = 1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "5", ["name"] = "F", ["level2"] = "FFF", ["parentId"] = "0", ["level"] = 1 } });
            var exceptions = topic.Context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }
    }
}