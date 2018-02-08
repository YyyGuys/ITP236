using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ERD
{
    public class Rubric
    {
        public List<Project> Projects = new List<Project>();
        public Rubric(string rubricPath)
        {
            XDocument xmlDoc = XDocument.Load(rubricPath);
            var projects = xmlDoc.Descendants("Project");
            foreach (var project in projects)
            {
                Projects.Add(LoadProject(project));
            }
        }
        #region LoadRubric
        private Project LoadProject(XElement project)
        {
            var proj = new Project();
            Projects.Add(proj);
            var entities = project.Descendants("BaseEntities");
            proj.BaseTables.AddRange(LoadTables(entities));

            entities = project.Descendants("RelationshipEntities");
            proj.ContextTables.AddRange(LoadTables(entities));
            proj.AllTables.AddRange(proj.BaseTables);
            proj.AllTables.AddRange(proj.ContextTables);
            LoadFks(proj, project.Descendants("BaseEntities"));
            LoadFks(proj, project.Descendants("RelationshipEntities"));

            proj.GeneralTests.AddRange(LoadGeneralTests(project.Descendants("GeneralTests")));
            return proj;
        }

        private List<GeneralTest> LoadGeneralTests(IEnumerable<XElement> generalTests)
        {
            var tests = new List<GeneralTest>();
            var genTests = generalTests.Descendants("Test");
            foreach (var test in genTests)
            {
                var name = test.Attribute("Name")?.Value;
                int type = Convert.ToInt16(test.Attribute("Type")?.Value);
                var desc = test.Attribute("Description")?.Value;
                var hint = test.Attribute("Hint")?.Value;
                tests.Add(new GeneralTest(name, type, desc, hint)
                {
                    Value = Convert.ToInt32(test.Attribute("Value").Value)
                });
            }
            return tests;
        }
        private List<RubricTable> LoadTables(IEnumerable<XElement> baseEntities)
        {
            List<RubricTable> tables = new List<RubricTable>();
            var entities = baseEntities.Descendants("Entity");
            foreach (var entity in entities)
            {
                var names = entity.Attribute("Name")?.Value;
                var value = entity.Attribute("Value")?.Value;

                var ent = new RubricTable(names);
                ent.Value = Convert.ToInt32(value ?? "0");
                ent.Columns.AddRange(LoadColumns(entity));
                ent.NfRules.AddRange(LoadNormalizationRules(entity));
                //ent.FKs.AddRange(LoadForeignKeys(ent, entity));
                //LoadInvalidColumnNames(ent, entity);
                LoadConstraints(ent, entity);
                tables.Add(ent);
            }
            return tables;
        }

        private void LoadFks(Project project, IEnumerable<XElement> entities)
        {
            var baseEntities = entities.Descendants("Entity");
            foreach (var entity in baseEntities)
            {
                var tableName = entity.Attribute("Name")?.Value;
                if (tableName == null) continue;

                var baseTable = project.AllTables.FirstOrDefault(at => at.Table.Name.Equals(tableName));
                if (baseTable == null) continue;

                var constraints = entity.Descendants("Constraints").FirstOrDefault();
                var fkGroup = constraints?.Descendants("ForeignKeys").FirstOrDefault();
                if (fkGroup == null) continue;

                var fks = fkGroup.Descendants("FK");
                foreach (var fk in fks)
                {
                    var table = fk.Descendants("Table").FirstOrDefault();
                    if (table == null) continue;
                    tableName = table.Value;
                    var foreignKey = new FK(tableName);
                    foreignKey.Table = FindTable(project.AllTables, foreignKey, tableName, fk);
                    baseTable.FKs.Add(foreignKey);
                }
            }
        }

        private RubricTable FindTable(IEnumerable<RubricTable> allTables, FK foreignKey, string tableName, XElement fk)
        {
            var table = allTables.FirstOrDefault(at => at.Table.Name.Equals(tableName));
            if (table != null)
            {
                foreignKey.Columns = FindColumns(table, fk);
            }
            return table;
        }

        private static List<RubricColumn> FindColumns(RubricTable table, XElement entity)
        {
            List<RubricColumn> tableColumns = new List<RubricColumn>();
            var column = entity.Descendants("Columns").First();
            var columns = column.Descendants("Column");
            foreach (var col in columns)
            {
                var names = col.Attribute("Name")?.Value.Split('|');
                if (names == null) continue;
                foreach (var name in names)
                {
                    var tableColumn = table.Columns.FirstOrDefault(tc => tc.Names.Contains(name));
                    if (tableColumn != null)
                    {
                        tableColumns.Add(tableColumn);
                        break;
                    }
                }
            }
            return tableColumns;
        }
        private List<RubricColumn> LoadColumns(XElement entity)
        {
            List<RubricColumn> rubricColumns = new List<RubricColumn>();
            var column = entity.Descendants("Columns").First();
            var columns = column.Descendants("Column");

            foreach (var col in columns)
            {
                var rubricColumn = new RubricColumn(col);
                rubricColumns.Add(rubricColumn);

            }
            return rubricColumns;
        }

        private List<RubricNormalization> LoadNormalizationRules(XElement entity)
        {
            var norms = new List<RubricNormalization>
            {
                LoadNorms(entity.Descendants("NF1").FirstOrDefault(), 1),
                LoadNorms(entity.Descendants("NF2").FirstOrDefault(), 2),
                LoadNorms(entity.Descendants("NF3").FirstOrDefault(), 3)
            };
            return norms;
        }

        private RubricNormalization LoadNorms(XElement entity, int nfLevel)
        {
            var nfRule = new RubricNormalization()
            {
                Level = nfLevel,
                Value = 0
            };
            if (entity == null) return nfRule;
            var isTestPk = entity.Attribute("TestPK");
            nfRule.IsTestPk = isTestPk != null;
            var names = entity.Descendants("Name");
            foreach (var name in names)
            {
                var whole = name.Attribute("IsWhole");
                bool isWhole = whole?.Value.Equals("1") ?? false;
                if (isWhole)
                    nfRule.WholeNames = name.Value.Split('|');
                else
                    nfRule.PartialNames = name.Value.Split('|');
            }
            return nfRule;
        }
        private void LoadInvalidColumnNames(RubricTable table, XElement entity)
        {
            var partialNames = new List<string>();
            var wholeNames = new List<string>();
            var invalids = entity.Descendants("Invalid");
            var invalidColumnNames = invalids.Descendants("Name");
            foreach (var invalid in invalidColumnNames)
            {
                var isWholeName = invalid.Attribute("IsWhole");
                bool isWhole = isWholeName?.Value.Equals("1") ?? false;
                if (isWhole)
                    wholeNames.AddRange(invalid.Value.Split('|'));
                else
                    partialNames.AddRange(invalid.Value.Split('|'));
            }
            table.InvalidPartialCols = partialNames.ToArray();
            table.InvalidWholeCols = wholeNames.ToArray();
        }

        private void LoadConstraints(RubricTable table, XElement entity)
        {
            var constraints = entity.Descendants("Constraints").FirstOrDefault();
            var pks = constraints.Descendants("PK").FirstOrDefault();
            table.PK.Columns.AddRange(FindColumns(table, pks));
            LoadAlternateKeys(table, constraints);
            //LoadForeignKeys(table, constraints);
        }

        private void LoadAlternateKeys(RubricTable table, XElement entity)
        {
            var alternateKeys = entity.Descendants("AlternateKeys");
            var aks = alternateKeys.Descendants("AK");
            foreach (var ak in aks)
            {
                var altKey = new AK
                {
                    Columns = FindColumns(table, ak),
                    Index = LoadIndex(table, ak)
                };
                table.AKs.Add(altKey);
            }
        }
        //private IEnumerable<FK> LoadForeignKeys(RubricTable table, XElement entity)
        //{
        //    var foreignKeys = entity.Descendants("ForeignKeys");
        //    var fks = foreignKeys.Descendants("FK");
        //    foreach (var fk in fks)
        //    {
        //        var name = fk.Attribute("Name")?.Value;
        //        var fkTable = fk.Attribute("Table")?.Value;
        //        var foreignKey = new FK(name);
        //        foreignKey.Columns = FindColumns(table, fk);
        //        //foreignKey.Table = FindTable(foreignKey, fkTable);
        //        table.FKs.Add(foreignKey);
        //    }
        //}

        private RubricIndex LoadIndex(RubricTable table, XElement entity)
        {
            var index = new RubricIndex();
            var element = entity.Descendants("Index").FirstOrDefault();
            if (element == null) return index;
            var isUnique = element?.Attribute("IsUnique");
            index.IsUnique = isUnique?.Value.Equals("1") ?? false;
            index.Columns = FindColumns(table, element);
            return index;
        }
        #region Project Classes
        public abstract class RubricTest
        {
            public virtual int Value { get; set; }
            public virtual int Grade { get; set; }
            public int Tests { get; set; }
            public int Violations { get; set; }
            public string Name { get; set; }
            public string Hint { get; set; }
            public List<string> Comments = new List<string>();
        }
        public class Project : RubricTest
        {
            public List<RubricTable> BaseTables = new List<RubricTable>();
            public List<RubricTable> ContextTables = new List<RubricTable>();
            public List<RubricTable> AllTables = new List<RubricTable>();

            public List<GeneralTest> GeneralTests = new List<GeneralTest>();
            public List<Table> TestTables = new List<Table>();

            public int GeneralTestValue => GeneralTests.Sum(gt => gt.Value);
            public int GeneralTestGrade => GeneralTests.Sum(gt => gt.Grade);

            public int TablesValue => AllTables.Sum(at => at.Value);
            public int TablesGrade => AllTables.Sum(at => at.Grade);

            public new int Value => GeneralTestValue + TablesValue;
            public new int Grade => GeneralTestGrade + TablesGrade;
            private ErModel Model { get; set; }
            public void TestErModel(ErModel model)
            {
                Model = model;
                TestTheTables();
                TestGeneralTests();
            }
            private void TestTheTables()
            {
                TestTheTables(BaseTables);
                TestTheTables(ContextTables);
            }
            private void TestTheTables(List<RubricTable> tables)
            {
                foreach (var rubricTable in tables)
                {
                    rubricTable.Grade = 0;           //--< Prove this is a valid table <<<
                    rubricTable.ErdTable = null;
                    int pointsOff = 0;
                    var comment = $"{rubricTable.Name} not found";
                    foreach (var modelTable in Model.Tables)
                    {
                        if (!rubricTable.AllowedNames.Contains(modelTable.Name)) continue;
                        comment = string.Empty;
                        pointsOff = TestColumns(rubricTable, modelTable);
                        if (!TestPK(rubricTable, modelTable))
                            pointsOff += (rubricTable.Value - pointsOff) / 2;
                        break;
                    }
                    if (!string.IsNullOrEmpty(comment))
                        rubricTable.Comments.Add(comment);
                    rubricTable.Grade = rubricTable.Value > pointsOff
                        ? rubricTable.Value - pointsOff
                        : 0;
                    if (!rubricTable.UnrecognizedColumns.Any()) continue;

                    var invalidCols = new StringBuilder($"The following columns are invalid in {rubricTable.ErdTable.Name}");
                    var delimiter = ':';
                    foreach (var col in rubricTable.UnrecognizedColumns)
                    {
                        invalidCols.Append($"{delimiter} {col}");
                        delimiter = ',';
                    }
                    rubricTable.Comments.Add(invalidCols.ToString());
                }
            }
            /// <summary>
            /// Tests to see if the PK is correct or not
            /// </summary>
            /// <param name="rubricTable">Rubric Table</param>
            /// <param name="erdTable">ERD Table (the one to test)</param>
            /// <returns>True if the ERD PK is valid; False if it is invalid</returns>
            private bool TestPK(RubricTable rubricTable, Table erdTable)
            {
                rubricTable.Tests++;
                var pk = erdTable.PrimaryKey;
                var rubricCount = rubricTable.PK.Columns.Count;

                if (rubricCount > pk.ConstraintColumns.Count) //--< Not enough PK columns. See if there is an AK <<<
                {
                    //--< TODO: Someday check the data types but for now check the column count >--//
                    var isRight = erdTable.Indices.Where(index => index.IsUnique)
                        .Any(index => index.Members.Count == rubricCount);
                    if (!isRight)
                        rubricTable.Comments.Add($"{rubricTable.Name} missing unique Alternate Key.");
                    return isRight;
                }
                //--< Is it the right number of PK columns? <<<
                if (rubricTable.PK.Columns.Count == pk.ConstraintColumns.Count) return true;
                rubricTable.Violations++;
                rubricTable.Comments.Add($"{rubricTable.Name} Primary Key is incorrect");
                return false;
            }
            /// <summary>
            /// Determines the "Points Off" from the Value
            /// </summary>
            /// <param name="rubricTable">The RubricTable to Compare</param>
            /// <param name="erdTable">The ERD Table to test</param>
            /// <returns>Returns the "Points Off" from the Value</returns>
            private int TestColumns(RubricTable rubricTable, Table erdTable)
            {
                rubricTable.ErdTable = erdTable;
                rubricTable.Tests = rubricTable.Columns.Count;
                var erdCols = erdTable.Columns;
                int validColumns = 0;
                foreach (var col in rubricTable.Columns)
                {
                    var names = UpperCase(col.Names);
                    foreach (var erdCol in erdCols)
                    {
                        if (!names.Contains(erdCol.Name.ToUpper())) continue;
                        col.Column = erdCol;
                        validColumns++;
                        break;
                    }
                }
                if (rubricTable.Columns.Count > validColumns)
                    rubricTable.Comments.Add($"{rubricTable.Name} missing key attributes");
                rubricTable.Violations = Math.Abs(rubricTable.Columns.Count - validColumns);

                //--< Test to see if any ERD Columns look odd & don't belong >--//
                foreach (var erdCol in rubricTable.ErdTable.Columns)
                {
                    rubricTable.Tests++;
                    var isValidCol = false;
                    foreach (var rubricCol in rubricTable.Columns)
                    {
                        if (!rubricCol.Names.Contains(erdCol.Name.ToUpper())) continue;
                        isValidCol = true;
                    }
                    if (isValidCol) continue;
                    rubricTable.Violations++;
                    rubricTable.UnrecognizedColumns.Add(erdCol.Name);
                }
                var gradedValue =
                    erdCols.Count == 0
                        ? 0
                        : (rubricTable.Tests - rubricTable.Violations) * rubricTable.Value / rubricTable.Tests;
                return rubricTable.Value - gradedValue;
            }
            private static string[] UpperCase(string[] names)
            {
                for (var i = 0; i < names.Length; i++)
                    names[i] = names[i].ToUpper();
                return names;
            }
            #region General Tests
            private void TestGeneralTests()
            {
                foreach (var test in GeneralTests)
                {
                    switch (test.Type)
                    {
                        case GeneralTest.TestType.Tables:
                            GeneralTableTest(test);
                            break;
                        case GeneralTest.TestType.DataTypes:
                            GeneralDataTypeTest(test);
                            break;
                        case GeneralTest.TestType.Attributes:
                            GeneralAttributeTest(test);
                            break;
                        case GeneralTest.TestType.PKs:
                            GeneralPKTest(test);
                            break;
                        case GeneralTest.TestType.Nf1:
                            GeneralNfTest(test, 1);
                            break;
                        case GeneralTest.TestType.Nf2:
                            GeneralNfTest(test, 2);
                            break;
                        case GeneralTest.TestType.Nf3:
                            GeneralNfTest(test, 3);
                            break;
                        case GeneralTest.TestType.Relationships:
                            GeneralRelationshipsTest(test);
                            break;
                    }
                }
            }
            private void GeneralTableTest(GeneralTest test)
            {
                //--< TODO: This needs more intensive testing >--//
                test.Tests = AllTables.Count;
                test.Violations = Math.Abs(test.Tests - Model.Tables.Count);
                test.Grade = test.Violations > 0
                    ? 0
                    : test.Value;
                if (test.Tests - Model.Tables.Count > 0)
                {
                    test.Comments.Add("You are missing one or more tables");
                }
                if (test.Tests - Model.Tables.Count < 0)
                {
                    test.Comments.Add("Your design is too complex");
                }
            }
            private void GeneralDataTypeTest(GeneralTest test)
            {
                int violations = 0;
                int columns = 0;
                foreach (var table in BaseTables)
                {
                    foreach (var column in table.Columns)
                    {
                        columns++;
                        if (column.Column == null)
                        {
                            violations++;
                            continue;
                        }
                        if (column.DataTypes.Contains(column.Column.DataType)) continue;
                        violations++;
                    }
                }
                test.Grade = DetermineGrade(test, columns, violations, $"{violations} or more data types are incorrect");
            }
            private void GeneralAttributeTest(GeneralTest test)
            {
                int violations = 0;
                int attributes = 0;
                foreach (var table in BaseTables)
                {
                    foreach (var column in table.Columns)
                    {
                        attributes++;
                        if (column.Column == null)
                            violations++;
                    }
                }
                test.Grade = DetermineGrade(test, attributes, violations, $"{violations} or more key attributes are missing");
            }

            private void GeneralPKTest(GeneralTest test)
            {
                int pks = 0;
                int violations = 0;
                foreach (var table in AllTables)
                {
                    pks++;
                    foreach (var pkColumn in table.PK.Columns)
                    {
                        if (pkColumn.Column == null)
                            violations++;
                    }
                }
                test.Grade = DetermineGrade(test, pks, violations, $"{violations} or more Primary Keys are incorrect");
            }

            private void GeneralRelationshipsTest(GeneralTest test)
            {
                int violations = 0, tests = 0;
                foreach (var table in AllTables)
                {
                    var modelTable = table.ErdTable;
                    foreach (var fk in table.FKs)
                    {
                        foreach (var fkCol in fk.Columns)
                        {
                            tests++;
                            if (fkCol.Column == null)
                                violations++;
                        }
                    }
                }
                violations += Math.Abs(Model.Tables.Sum(table => table.ForeignKeys.Count) - (tests - violations)); //--< Should not have more relationships than required <<<
                test.Grade = DetermineGrade(test, tests, violations, $"{violations} or more Relationships are incorrect");
            }
            private void GeneralNfTest(RubricTest test, int level)
            {
                int violations = 0, tests = 0;
                foreach (var table in AllTables)
                {
                    var rules = table.NfRules.Where(nf => nf.Level.Equals(level)).ToArray();
                    tests += rules.Length;
                    foreach (var rule in rules)
                    {
                        if (rule.IsTestPk && table.ErdTable?.PrimaryKey.ConstraintColumns.Count > 1)
                        {
                            violations++;
                            test.Comments.Add($"{table.Name} violates {level}NF because the PK is too complex");
                        }
                        if (rule.PartialNames != null)
                        {
                            bool isTableExists = true;
                            foreach (var partial in rule.PartialNames)
                            {
                                var regEx = new System.Text.RegularExpressions.Regex(partial);
                                if (table.ErdTable == null)
                                {
                                    if (isTableExists)
                                    {
                                        isTableExists = false;
                                        violations++;
                                        test.Comments.Add($"Reference table {table.Name} was not found");
                                    }
                                    continue;
                                }
                                if (!table.ErdTable.Columns.Any(col => regEx.IsMatch(col.Name))) continue;
                                violations++;
                                foreach (var col in table.ErdTable.Columns)
                                {
                                    if (!regEx.IsMatch(col.Name)) continue;
                                    rule.Violations.Add(col.Name);
                                    break;
                                }
                            }
                        }
                        if (rule.WholeNames.Any())
                        {
                            foreach (var partial in rule.WholeNames)
                            {
                                violations++;
                                foreach (var col in table.ErdTable.Columns)
                                {
                                    if (!partial.Contains(col.Name)) continue;
                                    rule.Violations.Add(col.Name);
                                    break;
                                }
                            }
                        }
                        if (!rule.Violations.Any()) continue;
                        var message = level == 1
                            ? "determines multiple values"
                            : "does not determine values";

                        var comment = new StringBuilder($"{table.Name} violates {level}NF because it {message} for");
                        var comma = ':';
                        foreach (var col in rule.Violations)
                        {
                            comment.Append($"{comma} {col}");
                            comma = ',';
                        }
                        test.Comments.Add(comment.ToString());
                    }
                }
                test.Grade = DetermineGrade(test, tests, violations, $"{violations} or more {level}NF Rules are broken");
            }
            #endregion
        }

        private static int DetermineGrade(RubricTest test, int tests, int violations, string failedTest = "")
        {
            test.Tests = tests;
            test.Violations = violations;
            if (violations > tests)
                test.Grade = 0;
            else
                test.Grade = tests == 0 || violations == 0
                    ? test.Value
                    : (tests - violations) * test.Value / tests;
            if (violations != 0)
                test.Comments.Add(failedTest);
            return test.Grade;
        }
        public class GeneralTest : RubricTest
        {
            public enum TestType
            {
                Tables = 1, DataTypes, Attributes, PKs, Nf1, Nf2, Nf3, Relationships
            }
            public string Name { get; set; }
            public TestType Type { get; set; }
            public string Description { get; set; }
            public string Hint { get; set; }
            public GeneralTest(string name, int testType, string description, string hint)
            {
                Name = name;
                Type = (TestType)testType;
                Description = description;
                Hint = hint;
            }
        }
        public class RubricTable : RubricTest
        {
            public Table Table { get; set; }
            public List<RubricColumn> Columns = new List<RubricColumn>();
            public string[] InvalidPartialCols { get; set; }
            public string[] InvalidWholeCols { get; set; }
            public PK PK = new PK();
            public List<AK> AKs = new List<AK>();
            public List<FK> FKs = new List<FK>();
            public List<RubricNormalization> NfRules = new List<RubricNormalization>();
            public List<string> UnrecognizedColumns = new List<string>();
            //public List<string> Comments = new List<string>();
            public ERD.Table ErdTable { get; set; }
            public string[] AllowedNames { get; set; }
            public RubricTable(string name)
            {
                AllowedNames = name.Split('|');
                Table = new Table()
                {
                    Name = AllowedNames[0]
                };
                Name = Table.Name;
            }
        }
        #region Constraints
        public abstract class RubricConstraint : RubricTest
        {
            public List<RubricColumn> Columns = new List<RubricColumn>();
        }
        public class PK : RubricConstraint
        {

        }

        public class AK : RubricConstraint
        {
            public RubricIndex Index;
        }

        public class FK : RubricConstraint
        {
            public RubricTable Table;
            public FK(string name)
            {
                Table = new RubricTable(name);
            }
        }
        #endregion
        public class RubricIndex : RubricTest
        {
            public bool IsUnique { get; set; }
            public List<RubricColumn> Columns = new List<RubricColumn>();
        }
        public class RubricColumn : RubricTest
        {
            public string[] Names { get; set; }
            public string[] DataTypes { get; set; }
            public bool IsNullable { get; set; }
            public Column Column { get; set; }

            public RubricColumn(XElement col)
            {
                Names = col.Attribute("Name").Value.Split('|');
                DataTypes = col.Attribute("DataType")?.Value.Split('|');
                var isNullable = col.Attribute("IsNullable");
                IsNullable = isNullable == null;
                if (isNullable != null)
                    IsNullable = isNullable.Value.Equals("1");
            }
        }

        public class RubricNormalization : RubricTest
        {
            public string[] WholeNames { get; set; }
            public string[] PartialNames { get; set; }
            public List<string> Violations = new List<string>();
            public int Level { get; set; }    //--< 1=1NF, 2=2NF, 3=3NF <<<
            public bool IsTestPk { get; set; }

            public RubricNormalization()
            {
                WholeNames = new string[0];
            }
        }
        #endregion
        #endregion
    }


}
