// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CSharpRecordsAnalyzer
{
    public static class LinqDebugExtensions
    {
        public static IEnumerable<T> Debug<T>(
            this IEnumerable<T> @this, Func<T, object> selector = null, [CallerLineNumber] int lineNum = 0)
        {
#if DEBUG
            // ReSharper disable once ExplicitCallerInfoArgument
            return Debug(@this, null, selector, lineNum);
#else
            return @this;
#endif
        }

        public static IEnumerable<T> Debug<T>(
            this IEnumerable<T> @this, string message = null, Func<T, object> selector = null,
            [CallerLineNumber] int lineNum = 0)
        {
#if DEBUG
            message = message ?? $"Debug (line {lineNum})";
            selector = selector ?? (x => x.ToString());
            return @this.Select(
                x => {
                    Console.WriteLine($"{message}: {selector(x)}");
                    return x;
                }
            );
#else
            return @this;
#endif
        }
    }
}
