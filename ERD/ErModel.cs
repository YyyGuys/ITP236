using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ERD
{
    public class ErModel
    {
        public List<Table> Tables { get; set; }

        public ErModel(string sql)
        {
            Tables = ParseSql(sql);
        }

        private List<Table> ParseSql(string sql)
        {
            var tables = new List<Table>();
            var lines = sql.Split('\t');
            Table table = null;
            Constraint constraint = null;
            foreach (var line in lines)
            {
                if (line.Contains("CREATE TABLE"))
                {
                    table = CreateTable(line);
                    tables.Add(table);
                }
                else if (line.Contains("CONSTRAINT"))
                {
                    constraint = AddConstraint(table, line);

                    if (constraint is PrimaryKey)
                        table.PrimaryKey = constraint as PrimaryKey;
                    else
                        table.ForeignKeys.Add(constraint as ForeignKey);
                }
                else if (line.StartsWith(" ["))
                {
                    table.Columns.Add(AddColumn(line));
                }
                else if (line.Contains("REFERENCES"))
                {
                    AddFkReference(tables, constraint as ForeignKey, line);
                }
            }
            return tables;
        }

        private Table CreateTable(string line)
        {
            var table = new Table();
            var start = line.IndexOf('[') + 1;
            table.Name = line.Substring(start, line.Length - start - 1);
            return table;
        }

        private Column AddColumn(string line)
        {
            var column = new Column();
            int start = 2;
            int end = line.IndexOf(']');
            column.Name = line.Substring(start, end - start);
            column.IsNullable = !line.Contains("NOT NULL");
            var shortLine = line.Substring(end + 2).Trim();
            end = shortLine.IndexOf(" ");
            column.DataType = shortLine.Substring(0, end);
            return column;
        }
        private Constraint AddConstraint(Table table, string line)
        {
            Constraint constraint;
            constraint = line.Contains("PRIMARY KEY")
                ? AddPrimaryKey(table, line)
                : AddForeignKey(table, line);
            int start = line.IndexOf("[") + 1;
            int end = line.IndexOf("]");
            constraint.Name = line.Substring(start, end - start);
            return constraint;
        }

        private Constraint AddPrimaryKey(Table table, string line)
        {
            var pk = new PrimaryKey();
            AddColumnsToConstraint(table, pk, line);
            return pk;
        }
        private Constraint AddForeignKey(Table table, string line)
        {
            var fk = new ForeignKey();
            AddColumnsToConstraint(table, fk, line);
            return fk;
        }

        private void AddFkReference(List<Table> tables, ForeignKey fk, string line)
        {
            int start = line.IndexOf("[") + 1;
            int end = line.IndexOf("]");
            string tableName = line.Substring(start, end - start);
            fk.Table = tables.First(tab => tab.Name.Equals(tableName));
        }
        private void AddColumnsToConstraint(Table table, Constraint constraint, string line)
        {
            int start = line.IndexOf(" ([") + 3;
            int position = 1;
            while (start > 3)
            {
                int end = line.IndexOf("]", start);
                var name = line.Substring(start, end - start);
                var column = table.Columns.Find(col => col.Name.Equals(name));
                if (column != null)
                {
                    constraint.ConstraintColumns.Add(new ConstraintColumn(position++, column));
                }
                start = line.IndexOf("[", start) + 1;
            }
        }

        #region Generate ERD
        public static XDocument GenerateERD(string sql)
        {
            var erd = new ErModel(sql);
            var xml = new XDocument();
            var elem = new XElement("Project");
            elem.Add(new XAttribute("Project", "0"));
            elem.Add(new XElement("Name", "..."));
            elem.Add(GenerateGeneralTests());
            elem.Add(GenerateEntities(erd));
            xml.Add(elem);
            return xml;
        }

        private static XElement GenerateGeneralTests()
        {
            var tests = new[]
            {
               new[] {"Tables", "10", "1", "Tables", "Too complex or too confusing"},
               new[] {"DataTypes", "10", "1", "Data Types", "Too complex or too confusing"},
               new[] {"Attributes", "10", "1", "Attributes", "Too complex or too confusing"},
               new[] {"PKs", "10", "1", "Primary Keys", "Too complex or too confusing"},
               new[] {"NF1", "10", "1", "1st Normal Form", "Too complex or too confusing"},
               new[] {"NF2", "10", "1", "2nd Normal Form", "Too complex or too confusing"},
               new[] {"NF3", "10", "1", "3rd Normal Form", "Too complex or too confusing"},
               new[] {"Relationships", "20", "1", "Relationships", "Too complex or too confusing"}
            };
            var elem = new XElement("GeneralTests");
            foreach (var test in tests)
            {
                var testElem = new XElement("Test");
                testElem.Add(new XAttribute("Name", test[0]));
                testElem.Add(new XAttribute("Value", test[1]));
                testElem.Add(new XAttribute("Type", test[2]));
                testElem.Add(new XAttribute("Description", test[3]));
                testElem.Add(new XAttribute("Hint", test[4]));
                elem.Add(testElem);
            }
            return elem;
        }

        private static XElement GenerateEntities(ErModel erd)
        {
            var elem = new XElement("BaseEntities");
            foreach (var table in erd.Tables)
            {
                var entity = GenerateEntity(table);
                elem.Add(entity);
            }
            return elem;
        }

        private static XElement GenerateEntity(Table table)
        {
            var elem = new XElement("Entity");
            elem.Add(new XAttribute("Name", table.Name));
            elem.Add(new XAttribute("Vaue", 10));
            var colElem = new XElement("Columns");
            foreach (var col in table.Columns)
            {
                colElem.Add(GenerateColumn(col));
            }
            elem.Add(colElem);
            GenerateEntityContext(elem);
            elem.Add(GenerateConstraints(table));
            return elem;
        }

        private static XElement GenerateConstraints(Table table)
        {
            var elem = new XElement("Constraints");
            elem.Add(GeneratePK(table));
            elem.Add(GenerateFKs(table));
            return elem;
        }

        private static XElement GeneratePK(Table table)
        {
            var elem = new XElement("PK");
            var cols = new XElement("Columns");
            foreach (var col in table.PrimaryKey.ConstraintColumns)
            {
                var colElem = new XElement("Column");
                colElem.Add(new XAttribute("Name", col.Column.Name));
                cols.Add(colElem);
            }
            elem.Add(cols);
            return elem;
        }

        private static XElement GenerateFKs(Table table)
        {
            var elem = new XElement("ForeignKeys");
            foreach (var fk in table.ForeignKeys)
            {
                var fkElem = new XElement("FK");
                fkElem.Add(new XElement("Table", fk.Table.Name));
                var cols = new XElement("Columns");
                foreach (var col in fk.ConstraintColumns)
                {
                    var colElem = new XElement("Column");
                    colElem.Add(new XAttribute("Name", col.Column.Name));
                    cols.Add(colElem);
                }
                fkElem.Add(cols);
                elem.Add(fkElem);
            }
            return elem;
        }
        private static void GenerateEntityContext(XElement entity)
        {
            for (var i = 0; i < 3; i++)
            {
                var nfElem = new XElement($"NF{i + 1}");
                var name = new XElement("Name");
                name.Add(new XAttribute("IsWhole", 1));
                nfElem.Add(name);

                name = new XElement("Name");
                nfElem.Add(name);
                entity.Add(nfElem);
            }
        }
        private static XElement GenerateColumn(Column column)
        {
            var elem = new XElement("Column");
            elem.Add(new XAttribute("Name", column.Name));
            elem.Add(new XAttribute("DataType", column.DataType));
            elem.Add(new XAttribute("IsNullable", column.IsNullable ? "1" : "0"));
            return elem;
        }
    }
    #endregion
    #region DDL
    public class Table
    {
        public string Name { get; set; }
        public List<Column> Columns { get; set; }
        public List<Index> Indices { get; set; }
        public PrimaryKey PrimaryKey { get; set; }
        public List<ForeignKey> ForeignKeys { get; set; }
        public Table()
        {
            Columns = new List<Column>();
            Indices = new List<Index>();
            ForeignKeys = new List<ForeignKey>();
        }
    }

    public class Column
    {
        public int Position { get; set; }
        public string Name { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsNullable { get; set; }
        public string DataType { get; set; }
    }

    public class Index
    {
        public string Name { get; set; }
        public bool IsClustered { get; set; }
        public bool IsUnique { get; set; }
        public bool IsPrimary { get; set; }
        public List<Member> Members { get; set; }

        public Index()
        {
            Members = new List<Member>();
        }
    }
    public class Member
    {
        public int Position { get; set; }
        public Column Column { get; set; }
    }
    public abstract class Constraint
    {
        public string Name { get; set; }
        public List<ConstraintColumn> ConstraintColumns { get; set; }

        protected Constraint()
        {
            ConstraintColumns = new List<ConstraintColumn>();
        }
    }
    public class PrimaryKey : Constraint
    {
    }
    public class ForeignKey : Constraint
    {
        public Table Table { get; set; }
    }

    public class ConstraintColumn
    {
        public int Position { get; set; }
        public Column Column { get; set; }

        public ConstraintColumn(int position, Column column)
        {
            Position = position;
            Column = column;
        }
    }
    #endregion
}
