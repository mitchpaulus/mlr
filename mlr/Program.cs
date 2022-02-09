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
        static int Main(string[] args)
        {
            string dataFilePath = null;

            bool addConstant = true;
            bool printResults = false;
            string printResultsFile = null;
            bool printStats = false;

            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a is "-h" or "--help")
                {
                    DisplayHelp();
                    return 1;
                }
                else if (a is "-n" or "--no-const") addConstant = false;
                else if (a is "-s" or "--stats") printStats = true;
                else if (a is "-p" or "--print") printResults = true;
                else
                {
                    dataFilePath = a;
                }
            }
            
            string input;
            if (dataFilePath is null or "-")
            {
                input = Console.In.ReadToEnd();
            }
            else if (args.Length > 0)
            {
                FileInfo fileInfo = new FileInfo(dataFilePath);
                if (!fileInfo.Exists)
                {
                    Console.Write($"Could not find the file '{fileInfo.FullName}'");
                    return 1;
                }
                
                input = File.ReadAllText(dataFilePath);
            }
            else
            {
                Console.Out.WriteLine("No input.");
                return 1;
            }

            if (input.Length == 0)
            {
                Console.Out.Write($"No input read from '{dataFilePath}'.\n");
                return 1;
            }

            List<string> lines = input.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None ).ToList();

            // Split on whitespace
            List<string[]> splitLines = lines.Select(line => line.Split(null)).ToList();

            IEnumerable<IGrouping<int, string[]>> columnCounts = splitLines.GroupBy(cols => cols.Length).ToList();

            int mostCommonColumnLength = columnCounts.OrderByDescending(grouping => grouping.Count()).First().Key;

            List<List<double>> data = splitLines
                .Where(line => IsValidLine(line, mostCommonColumnLength))
                .Select(datas => datas.Select(double.Parse).ToList()).ToList();

            if (!data.Any())
            {
                Console.Out.WriteLine($"None of the original {splitLines.Count} lines contained records of completely clean data.");
                return 1;
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

            RegressionOutputs outputs = Regression.MultipleLinearRegression(yArray, xArray, printStats, addConstant);

            foreach (double coefficient in outputs.Coeffs)
            {
                int numberOfSignificantFigures = Math.Min(10, Math.Max(0,  5 - (int)Math.Floor(Math.Log10(Math.Abs(coefficient)))));
                Console.Out.Write($"{coefficient.ToString($"f{numberOfSignificantFigures}").TrimZeros()}\n");
            }

            if (printStats)
            {
                Console.Write($"CV (%): {outputs.CV * 100}\n");
                Console.Write($"n: {outputs.Ydata.Length:D}\n");
                Console.Write($"R2: {outputs.Rsquared}\n");
                Console.Write($"R2 adj: {outputs.AdjRsquared}\n");
                Console.Write($"t-stats: {string.Join(", ", outputs.Tstats.ToList())}\n");
                Console.Write($"SSR Σ(y_pred - y_ave)²: {outputs.SSR}\n");
                Console.Write($"SSE Σ(y_meas - y_pred)²: {outputs.SSE}\n");
                Console.Write($"SST Σ(y_meas - y_ave)²: {outputs.SST}\n");
                Console.Write($"Average Y: {outputs.YAvg}\n");
                Console.Write($"Standard Error: {outputs.StandardError}\n");
            }

            return 0;
        }

        public static void DisplayHelp()
        {
            Console.Write("mlr\n");
            Console.Write("\n");
            Console.Write("USAGE:\n");
            Console.Write("  mlr [options]... datafile\n");
            Console.Write("\n");
            Console.Write("OPTIONS:\n");
            Console.Write(" -h, --help      Show help and exit\n");
            Console.Write(" -n, --no-const  Don't add constant for regression coefficients\n");
            Console.Write("\n");
            Console.Write("The 'dataFile' is expected to be a whitespace delimited data file,\n");
            Console.Write("with the first column being Y, followed by X1, X2, ... columns.\n");
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
            return string.Concat(numberInput.Reverse().SkipWhile(c => c is '0' or '.').Reverse());
        }
    }
}
