using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ScriptVerifier
{
    public class DefaultCompilerSetup : CompilerSetup
    {
        private static readonly List<Assembly> DefaultAllowedAssemblies =
            new List<Assembly>
            {
                typeof(string).Assembly,
                typeof(object).Assembly, // "mscorlib.dll"
                typeof(Uri).Assembly, // "System.dll"
                typeof(Enumerable).Assembly, // "System.Core.dll"
            };

        private static readonly List<Type> DefaultAllowedTypes =
            new List<Type>
            {
                typeof(object),
                typeof(bool),
                typeof(char),
                typeof(double),
                typeof(float),
                typeof(int),
                typeof(long),
                typeof(string),
                typeof(Enum),
                typeof(Guid),
                typeof(DateTime),
                typeof(Match),
                typeof(Regex),
                typeof(TimeSpan),
                typeof(Math),
                typeof(System.Diagnostics.Stopwatch),
                typeof(System.Text.StringBuilder),

                typeof(Nullable<>),

                typeof(IEnumerable<>),
                typeof(IList<>),
                typeof(ICollection<>),
                typeof(ISet<>),
                typeof(ILookup<,>),
                typeof(IGrouping<,>),
                typeof(IDictionary<,>),
                typeof(IReadOnlyDictionary<,>),
                typeof(IOrderedEnumerable<>),
                typeof(Action<>),
                typeof(Func<>),
                typeof(Func<,>),
                typeof(KeyValuePair<,>),

                typeof(Comparer<>),

                typeof(Tuple),
                typeof(Tuple<>),
                typeof(Tuple<,>),
                typeof(Tuple<,,>),
                typeof(Tuple<,,,>),
                typeof(Tuple<,,,,>),
                typeof(Tuple<,,,,,>),
                typeof(Tuple<,,,,,,>),
                typeof(Tuple<,,,,,,,>),

                typeof(ValueTuple),
                typeof(ValueTuple<>),
                typeof(ValueTuple<,>),
                typeof(ValueTuple<,,>),
                typeof(ValueTuple<,,,>),
                typeof(ValueTuple<,,,,>),
                typeof(ValueTuple<,,,,,>),
                typeof(ValueTuple<,,,,,,>),
                typeof(ValueTuple<,,,,,,,>),

                typeof(List<>),
                typeof(Dictionary<,>),
                typeof(SortedDictionary<,>),
                typeof(HashSet<>),

                typeof(Enumerable),
                typeof(ParallelEnumerable)
            };

        public DefaultCompilerSetup()
        {
            AddReferencedAssemblies(DefaultAllowedAssemblies);
            AddAllowedTypes(DefaultAllowedTypes, true);
        }
    }
}