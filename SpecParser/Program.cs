
using System.Text;
using System.Xml.Linq;

namespace SpecParser;

public static class Program
{
    public static void Main(string[] args)
    {
        // -----
        var specFile = new FileInfo(@"C:\workspace\repos\OpenGL-Registry\xml\gl.xml");
        // -----
        var enumName = "DebugSeverity";
        // -----

        var doc = XDocument.Load(specFile.FullName);

        var sb = new StringBuilder();

        sb.AppendLine($"    public enum {enumName} : uint");
        sb.AppendLine($"    {{");

        foreach (var enumElement in doc.Descendants("enum"))
        {
            var name = enumElement.Attribute("name")?.Value;
            var value = enumElement.Attribute("value")?.Value;
            var group = enumElement.Attribute("group")?.Value;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(group)) continue;

            if (group.Contains(enumName))
            {
                sb.AppendLine($"        {CleanName(name)} = {value},");
            }
        }

            sb.AppendLine($"    }}");

        Console.WriteLine("==== start ====");
        Console.WriteLine(sb.ToString());
        Console.WriteLine("==== end ====");
        Console.ReadLine();
    }

    private static string CleanName(string name)
    {
        return name.Replace("GL_", "").Replace("GLU_", "").Replace("GLX_", "").Replace("WGL_", "");
    }

    private static string CleanValue(string value)
    {
        if (value.StartsWith("0x"))
        {
            return Convert.ToInt32(value, 16).ToString();
        }
        return value;
    }
}