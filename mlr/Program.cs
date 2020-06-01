using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace mlr
{
    class Program
    {
        static void Main(string[] args)
        {
            string input;
            if (Console.IsInputRedirected)
            {
                input = Console.In.ReadToEnd();
            }
            else if (args.Length > 0)
            {
                input = File.ReadAllText(args[0]);
            }
            else
            {
                Console.Out.WriteLine("No input.");
                return;
            }

            if (input.Length == 0)
            {
                Console.Out.WriteLine("No input.");
                return;
            }

            List<string> lines = input.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None ).ToList();

            List<string[]> splitLines = lines.Select(line => line.Split(null)).ToList();

            IEnumerable<IGrouping<int, string[]>> columnCounts = splitLines.GroupBy(cols => cols.Length).ToList();

            int mostCommonColumnLength = columnCounts.OrderByDescending(grouping => grouping.Count()).First().Key;

            List<List<double>> data = splitLines
                .Where(line => IsValidLine(line, mostCommonColumnLength))
                .Select(datas => datas.Select(double.Parse).ToList()).ToList();

            if (!data.Any())
            {
                Console.Out.WriteLine($"None of the original {splitLines.Count} lines contained records of completely clean data.");
                return;
            }

            double[,] xArray = new double[data.Count,mostCommonColumnLength - 1];
            double[]  yArray = new double[data.Count];

            foreach ((List<double> record, int rowIndex) in data.WithIndex())
            {
                foreach ((double dataPoint, int colIndex) in record.Skip(1).WithIndex())
                {
                    xArray[rowIndex, colIndex] = dataPoint;
                }

                yArray[rowIndex] = record[0];
            }

            RegressionOutputs outputs = Regression.MultipleLinearRegression(yArray, xArray, false);

            foreach (double coefficient in outputs.Coeffs)
            {
                int numberOfSignificantFigures = Math.Min(10, Math.Max(0,  5 - (int)Math.Floor(Math.Log10(Math.Abs(coefficient)))));
                Console.Out.WriteLine($"{Math.Round(coefficient, numberOfSignificantFigures).ToString($"f{numberOfSignificantFigures}").TrimZeros()}");
            }
        }
        public static bool IsValidLine(string[] strings, int numColumns) => strings.Any() && strings.Length == numColumns && strings.All(s => double.TryParse(s, out double _)); 

    }


    public static class Extensions
    {
        public static IEnumerable<(T list, int index)> WithIndex<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.Select((item, i) => (item, i));
        }

        public static string TrimZeros(this string numberInput)
        {
            return string.Concat(numberInput.Reverse().SkipWhile(c => c == '0' || c == '.').Reverse());
        }
    }
}
