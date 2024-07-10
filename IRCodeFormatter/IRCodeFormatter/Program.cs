using System.IO;

if (args.Length == 0)
{
    Console.WriteLine("Argument is Empty");
    return;
}
string ReadFilePath = args[0];
if (!File.Exists(ReadFilePath))
{
    Console.WriteLine("File not found!");
    return;
}
string Formatted;
using (StreamReader sr = new StreamReader(ReadFilePath))
{
    string code = sr.ReadToEnd();
    Formatted = code.Replace("pulse ", "").Replace("space ", "").Replace("\r\n", ",");
}
string NewFileName = Path.Combine(Directory.GetParent(ReadFilePath).FullName, Path.GetFileNameWithoutExtension(ReadFilePath) + "-formatted.txt");
Console.WriteLine(NewFileName);
using (StreamWriter  sw = new StreamWriter(NewFileName, false))
{
    sw.Write(Formatted);
}