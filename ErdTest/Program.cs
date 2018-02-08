using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ERD;

namespace ErdTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var sql = new StringBuilder();
            using (var file = System.IO.File.OpenText("C:\\Users\\bob.dust\\Documents\\Personal\\Sql.txt"))
            {
                // Read file
                while (!file.EndOfStream)
                {
                    String line = file.ReadLine();

                    // Ignore empty lines
                    if (line.Length > 0)
                    {
                        // Create addon
                        sql.Append(line);
                        sql.Append("\t");
                    }
                }
            }
            var xml = ErModel.GenerateERD(sql.ToString());

            var erd = new ErModel(sql.ToString());
            var rubric = new Rubric("XML\\Rubric.xml");
            var project = rubric.Projects[0];
            project.TestErModel(erd);
            ShowResults(project);
            Console.ReadLine();
        }

        private static void ShowResults(Rubric.Project project)
        {
            Console.WriteLine("Table\tGrade\tTotal");
            foreach (var table in project.AllTables)
            {
                Console.WriteLine($"{table.Name}\t{table.Grade}\t{table.Value}");
                DisplayComments(table.Comments);
            }
            Console.WriteLine("");
            Console.WriteLine("< --------------------------------------- >");
            foreach (var test in project.GeneralTests)
            {
                Console.WriteLine($"{test.Name}\t{test.Grade}\t{test.Value}");
                DisplayComments(test.Comments);
            }
            Console.WriteLine($"Total\t{project.Grade}\t{project.Value}");
        }

        private static void DisplayComments(List<string> comments)
        {
            foreach (var comment in comments)
            {
                Console.WriteLine($"\t\t\t\t{comment}");
            }
        }
    }
}
