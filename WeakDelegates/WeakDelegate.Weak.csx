using System;
using System.Reflection;
using System.Linq;

public static string GetFriendlyName(this Type type)
{
    string friendlyName = type.Name;
    if (type.IsGenericType)
    {
        int iBacktick = friendlyName.IndexOf('`');
        if (iBacktick > 0)
        {
            friendlyName = friendlyName.Remove(iBacktick);
        }
        friendlyName += "<";
        Type[] typeParameters = type.GetGenericArguments();
        for (int i = 0; i < typeParameters.Length; ++i)
        {
            string typeParamName = typeParameters[i].Name;
            friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
        }
        friendlyName += ">";
    }

    return friendlyName;
}

Output.WriteLine("using static WeakDelegates.WeakDelegate;");

foreach (IGrouping<string, Type> typesInNamespace in (new[] { Assembly.GetAssembly(typeof(int)), Assembly.LoadWithPartialName("System"), Assembly.LoadWithPartialName("System.Core"), Assembly.LoadWithPartialName("Microsoft.CSharp") }).SelectMany(a => a.GetExportedTypes()).Where(t => t != typeof(MulticastDelegate) && typeof(MulticastDelegate).IsAssignableFrom(t) && t.IsPublic).GroupBy(t => t.Namespace))
{
    Output.WriteLine($"namespace {typesInNamespace.Key}{Environment.NewLine}{{{Environment.NewLine}\tpublic static class WeakDelegateHelpers{Environment.NewLine}\t{{");
    foreach(Type type in typesInNamespace)
    {
        string name = type.GetFriendlyName();
        string args = type.IsGenericType ? name.Substring(name.IndexOf('<')) : string.Empty;
        string notClsCompliant = (type.GetCustomAttribute<CLSCompliantAttribute>()?.IsCompliant ?? true) ? string.Empty : "[System.CLSCompliant(false)]";
        Output.WriteLine($"\t\t{notClsCompliant}public static {name} Weak{args}({name} @delegate) => Combine(null, @delegate);");
    }
    Output.WriteLine($"\t}}{Environment.NewLine}}}");
}