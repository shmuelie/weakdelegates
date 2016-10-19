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

Output.WriteLine("namespace WeakDelegates{public static partial class WeakDelegate{");

foreach (Type type in (new[] { Assembly.GetAssembly(typeof(int)), Assembly.LoadWithPartialName("System"), Assembly.LoadWithPartialName("System.Core"), Assembly.LoadWithPartialName("Microsoft.CSharp") }).SelectMany(a => a.GetExportedTypes()).Where(t => t != typeof(MulticastDelegate) && typeof(MulticastDelegate).IsAssignableFrom(t) && t.IsPublic))
{
    string name = type.GetFriendlyName();
    string args = type.IsGenericType ? name.Substring(name.IndexOf('<')) : string.Empty;
    string notClsCompliant = (type.GetCustomAttribute<CLSCompliantAttribute>()?.IsCompliant ?? true) ? string.Empty : "[System.CLSCompliant(false)]";
    Output.WriteLine($"{notClsCompliant}public static {type.Namespace}.{name} Weak{args}({type.Namespace}.{name} @delegate) => Combine(null, @delegate);");
}
Output.WriteLine("}}");