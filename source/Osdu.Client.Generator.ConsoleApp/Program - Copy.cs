//using System.Runtime.CompilerServices;

//namespace Osdu.Client.Generator.ConsoleApp;

//internal class Program
//{
//    static void Main(string[] args)
//    {
//        string outputDir = GetOutputDirectory(); 
        
//        Directory.CreateDirectory(outputDir);

//        // Generate code files
//        var generatedFiles = CodeGenerator.Generate();

//        foreach (var (fileName, content) in generatedFiles)
//        {
//            var filePath = Path.Combine(outputDir, fileName);
//            File.WriteAllText(filePath, content);
//            Console.WriteLine($"Generated: {filePath}");
//        }

//        Console.WriteLine($"Done. {generatedFiles.Count} file(s) generated.");
//    }
//    static string GetOutputDirectory([CallerFilePath] string sourceFilePath = "")
//    {
//        string sourceFileDir = Path.GetDirectoryName(sourceFilePath)!;
//        string parentDir = Directory.GetParent(sourceFileDir).FullName!;

//        return Path.Combine(parentDir, "Osdu.Client", "Generated");
//    }
//}
