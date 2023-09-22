using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            string pythonClassName = "Model";
            int skip = 0;

            string delimiter = null;

            OutputFormatter formatter = new TextOutputter();

            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a is "-h" or "--help")
                {
                    DisplayHelp();
                    return 0;
                }

                if (a is "-v" or "--version")
                {
                    Console.Write(Version + "\n");
                    return 0;
                }

                if (a is "-d" or "--delim")
                {
                    // Check for next argument
                    if (i + 1 >= args.Length)
                    {
                        Console.Write("Missing delimiter argument\n");
                        return 1;
                    }

                    delimiter = args[++i];
                }
                else if (a is "-n" or "--no-const") addConstant = false;
                else if (a is "-s" or "--stats") printStats = true;
                else if (a is "-p" or "--print") printResults = true;
                else if (a is "--skip")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Write("Missing skip argument\n");
                        return 1;
                    }

                    if (!int.TryParse(args[++i], out skip))
                    {
                        Console.Write("Invalid skip argument\n");
                        return 1;
                    }
                    else
                    {
                        if (skip < 0)
                        {
                            Console.Write("Invalid skip argument\n");
                            return 1;
                        }
                    }
                }
                else if (a is "--json") formatter = new JsonOutputter();
                else if (a == "--python")
                {
                    printStats = true;
                    formatter = new PythonFormatter();
                }
                else if (a == "--class-name") {
                    // Check that next argument is present
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("--class-name requires an argument");
                        return 1;
                    }
                    pythonClassName = args[i + 1];
                    i++;
                }
                else
                {
                    dataFilePath = a;
                }
            }

            string input;
            if (dataFilePath is null or "-")
            {
                try { input = Console.In.ReadToEnd(); }
                catch
                {
                    Console.Error.Write("Ran into exception reading from Standard Input.\n");
                    return 1;
                }
            }
            else if (args.Length > 0)
            {
                FileInfo fileInfo = new(dataFilePath);
                if (!fileInfo.Exists)
                {
                    Console.Error.Write($"Could not find the file '{fileInfo.FullName}'");
                    return 1;
                }

                input = File.ReadAllText(dataFilePath);
            }
            else
            {
                Console.Error.Write("No input.\n");
                return 1;
            }

            if (input.Length == 0)
            {
                Console.Error.Write($"No input read from '{dataFilePath}'.\n");
                return 1;
            }

            List<string> lines = input.Split( new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None ).ToList();
            if (lines.Count < 2)
            {
                Console.Error.Write($"Only found a single line.\n");
                return 1;
            }

            // Split on whitespace by default (null), otherwise whatever was put in by user.
            List<string[]> splitLines;

            if (dataFilePath != null)
            {
                if (dataFilePath.ToLower().EndsWith(".csv")) delimiter = ",";
                else if (dataFilePath.ToLower().EndsWith(".tsv")) delimiter = "\t";
            }
            
            // Check for presence of tab. If tab found, assume tab delimited.
            if (lines[0].Contains('\t')) delimiter = "\t";
            
            if (delimiter is null) splitLines = lines.Skip(skip).Select(line => line.Split()).ToList();
            else                   splitLines = lines.Skip(skip).Select(line => line.Split(delimiter)).ToList();

            IEnumerable<IGrouping<int, string[]>> columnCounts = splitLines.GroupBy(cols => cols.Length).ToList();

            int mostCommonColumnLength = columnCounts.OrderByDescending(grouping => grouping.Count()).First().Key;

            List<List<double>> data = splitLines
                .Where(line => IsValidLine(line, mostCommonColumnLength))
                .Select(datas => datas.Select(double.Parse).ToList()).ToList();

            if (!data.Any())
            {
                Console.Error.Write($"None of the original {splitLines.Count} lines contained records of completely clean data.\n");
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

            if (formatter is PythonFormatter pythonFormatter)
            {
                Console.Out.Write(pythonFormatter.Format(outputs, pythonClassName));
            }
            else
            {
                foreach (double coefficient in outputs.Coeffs)
                {
                    int numberOfSignificantFigures = Math.Min(10, Math.Max(0, 5 - (int)Math.Floor(Math.Log10(Math.Abs(coefficient)))));
                    Console.Out.Write($"{coefficient.ToString($"f{numberOfSignificantFigures}").TrimZeros()}\n");
                }

                if (printStats)
                {
                    Console.Out.Write(formatter.Format(outputs, pythonClassName));
                }
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
            Console.Write("     --class-name <name> Name of the Python class to generate.\n");
            Console.Write(" -h, --help      Show help and exit\n");
            Console.Write("     --json      Format output as JSON\n");
            Console.Write(" -n, --no-const  Don't add constant for regression coefficients\n");
            Console.Write("     --python    Format output as Python\n");
            Console.Write(" -s, --stats     Print regression statistics\n");
            Console.Write("     --version   Display version and exit\n");
            Console.Write("\n");
            Console.Write("The 'dataFile' is expected to be a whitespace delimited data file,\n");
            Console.Write("with the first column being Y, followed by X1, X2, ... columns.\n");
            Console.Write("\n");
            Console.Write("Input can be read from standard input, using '-' as the data file.\n");
        }

        private static string Version => "0.1.0";

        public static bool IsValidLine(string[] strings, int numColumns) => strings.Any() && strings.Length == numColumns && strings.All(s => double.TryParse(s, out double _));
    }

    public interface OutputFormatter
    {
        public string Format(RegressionOutputs outputs, string className);
    }

    public enum OutputterType
    {
        Text,
        JSON,
    }

    public class OutputterFactory
    {

        public static OutputFormatter Create(OutputterType type)
        {
            switch (type)
            {
                case OutputterType.Text:
                    return new TextOutputter();
                case OutputterType.JSON:
                    return new JsonOutputter();
                default:
                    throw new ArgumentException($"Unknown outputter type '{type}'");
            }
        }
    }

    public class TextOutputter : OutputFormatter
    {
        public string Format(RegressionOutputs outputs, string className)
        {
            StringBuilder builder = new();

            builder.Append($"CV (%): {outputs.CV * 100}\n");
            builder.Append($"n: {outputs.Ydata.Length:D}\n");
            builder.Append($"R2: {outputs.Rsquared}\n");
            builder.Append($"R2 adj: {outputs.AdjRsquared}\n");
            builder.Append($"t-stats: {string.Join(", ", outputs.Tstats.ToList())}\n");
            builder.Append($"SSR Σ(y_pred - y_ave)²: {outputs.SSR}\n");
            builder.Append($"SSE Σ(y_meas - y_pred)²: {outputs.SSE}\n");
            builder.Append($"SST Σ(y_meas - y_ave)²: {outputs.SST}\n");
            builder.Append($"Average Y: {outputs.YAvg}\n");
            builder.Append($"Standard Error: {outputs.StandardError}\n");

            if (outputs.Xdata != null && outputs.Xdata.GetLength(0) > 0)
            {
                for (var i = 0; i < outputs.Xdata.GetLength(1); i++)
                {
                    var dataForColumn = outputs.Xdata.DataForColumn(i);
                    var minValue = dataForColumn.Min();
                    var maxValue = dataForColumn.Max();
                    builder.Append($"X{i}: {minValue} - {maxValue}\n");
                }
            }

            return builder.ToString();
        }
    }

    public class JsonOutputter : OutputFormatter
    {
        public string Format(RegressionOutputs outputs, string className)
        {
            return outputs.ToJson();
        }
    }

    public class PythonFormatter : OutputFormatter
    {
        public string Format(RegressionOutputs outputs, string className)
        {
            StringBuilder builder = new();

            builder.Append($"class {className}:\n");
            builder.Append("  coeffs = [\n");
            foreach (double coefficient in outputs.Coeffs)
            {
                builder.Append($"    {coefficient},\n");
            }
            builder.Append("  ]\n");
            builder.Append($"  cv = {outputs.CV}\n");
            builder.Append($"  n = {outputs.n}\n");
            builder.Append($"  r2 = {outputs.Rsquared}\n");
            builder.Append($"  r2_adj = {outputs.AdjRsquared}\n");
            builder.Append($"  t_stats = [{string.Join(", ", outputs.Tstats.ToList())}]\n");
            builder.Append($"  ssr = {outputs.SSR}\n");
            builder.Append($"  sse = {outputs.SSE}\n");
            builder.Append($"  sst = {outputs.SST}\n");
            builder.Append($"  y_ave = {outputs.YAvg}\n");
            builder.Append($"  std_err = {outputs.StandardError}\n");

            return builder.ToString();
        }
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

        public static List<double> DataForColumn(this double[,] allData, int column)
        {
            if (column < 0 || column >= allData.GetLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(column), $"Column index out of range. Must be between 0 and the number of columns in the data ({allData.GetLength(1)}). Entered {column}");
            }

            var data = new List<double>();

            for (var i = 0; i < allData.GetLength(0); i++)
            {
                data.Add(allData[i, column]);
            }

            return data;
        }
    }
}
