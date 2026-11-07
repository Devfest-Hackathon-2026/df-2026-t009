using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExCSS;

public static class CssSyntaxCheck
{
    public static int Main(string[] args)
    {
        var errors = new List<string>();
        var parser = new StylesheetParser(includeUnknownRules: true, includeUnknownDeclarations: true,
            errorHandler: (sender, error) => errors.Add(error.Message));
        foreach (var file in args)
        {
            var css = File.ReadAllText(file);
            var sheet = parser.Parse(css);
            Console.WriteLine(Path.GetFileName(file) + ": " + sheet.StyleRules.Count() + " style rules, " + sheet.ImportRules.Count() + " imports.");
        }
        foreach (var error in errors) Console.Error.WriteLine(error);
        Console.WriteLine(errors.Count == 0 ? "PASS: Unity's ExCSS tokenizer reports no stylesheet syntax errors. USS asset resolution and style semantics require Unity import." : "FAIL: stylesheet tokenizer errors: " + errors.Count);
        return errors.Count == 0 ? 0 : 1;
    }
}

