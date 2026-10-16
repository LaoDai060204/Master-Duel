using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../Assets"));
if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
var common = new List<string> { "UNITY_6000_6", "ENABLE_INPUT_SYSTEM", "NET_STANDARD_2_0", "NET_STANDARD_2_1" };
for (int minor = 1; minor <= 6; minor++) common.Add($"UNITY_5_{minor}_OR_NEWER");
for (int year = 2017; year <= 2023; year++)
    for (int minor = 1; minor <= 4; minor++) common.Add($"UNITY_{year}_{minor}_OR_NEWER");
for (int minor = 0; minor <= 6; minor++) common.Add($"UNITY_6000_{minor}_OR_NEWER");
var profiles = new Dictionary<string, string[]>
{
    ["EditorLinux"] = new[] { "UNITY_EDITOR", "UNITY_EDITOR_LINUX", "UNITY_STANDALONE", "UNITY_STANDALONE_LINUX" },
    ["Windows"] = new[] { "UNITY_STANDALONE", "UNITY_STANDALONE_WIN" },
    ["Android"] = new[] { "UNITY_ANDROID" }
};

int errors = 0;
foreach (var profile in profiles)
{
    var options = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: common.Concat(profile.Value));
    int files = 0;
    foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
    {
        byte[] bytes = File.ReadAllBytes(path);
        string source;
        try { source = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\ufeff'); }
        catch (DecoderFallbackException) { source = Encoding.GetEncoding("GB18030").GetString(bytes); }
        var tree = CSharpSyntaxTree.ParseText(source, options, path);
        foreach (var diagnostic in tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
        {
            Console.Error.WriteLine(profile.Key + ": " + diagnostic);
            errors++;
        }
        files++;
    }
    Console.WriteLine($"{profile.Key}: parsed {files} C# source files.");
}
Console.WriteLine($"Syntax errors: {errors}. Parsing does not check types, references or Unity asset import.");
return errors == 0 ? 0 : 1;
