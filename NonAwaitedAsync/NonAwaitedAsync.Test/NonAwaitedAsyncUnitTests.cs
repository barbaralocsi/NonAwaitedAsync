using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = NonAwaitedAsync.Test.CSharpCodeFixVerifier<
    NonAwaitedAsync.NonAwaitedAsyncAnalyzer,
    NonAwaitedAsync.NonAwaitedAsyncCodeFixProvider>;

namespace NonAwaitedAsync.Test
{
    [TestClass]
    public class NonAwaitedAsyncUnitTest
    {


        [TestMethod]
        public async Task AwaitedAsyncTask_NoDiagnostic()
        {
            const string code = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        await GetValueAsync(1);
        Console.WriteLine(3);
    }

    private static async Task GetValueAsync(int numberToAdd)
    {
        await Task.Run(() => numberToAdd * 2);
    }
}

";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task AwaitedAsyncTaskInt_NoDiagnostic()
        {
            const string code = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        var a = await GetValueAsync(1);
        Console.WriteLine(a);
    }

    private static async Task<int> GetValueAsync(int numberToAdd)
    {
        return await Task.Run(() => numberToAdd * 2);
    }
}

";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task NonAwaitedAsyncTask_ProducesDiagnostic()
        {

            const string code = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        var a = GetValueAsync(1);
        Console.WriteLine(a);
    }

    private static async Task GetValueAsync(int numberToAdd)
    {
        await Task.Run(() => numberToAdd * 2);
    }
}

";

            var expected = VerifyCS.Diagnostic("NonAwaitedAsync")
                .WithMessage("Async call should be awaited")
                .WithSpan(9, 17, 9, 33);

            await VerifyCS.VerifyAnalyzerAsync(code, expected);
        }

        [TestMethod]
        public async Task NonAwaitedAsyncTaskInt_ProducesDiagnostic()
        {

            const string code = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        var a = GetValueAsync(1);
        Console.WriteLine(a);
    }

    private static async Task<int> GetValueAsync(int numberToAdd)
    {
        return await Task.Run(() => numberToAdd * 2);
    }
}

";

            var expected = VerifyCS.Diagnostic("NonAwaitedAsync")
                .WithMessage("Async call should be awaited")
                .WithSpan(9, 17, 9, 33);

            await VerifyCS.VerifyAnalyzerAsync(code, expected);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public async Task CustomTaskClass_NoDiagnostics()
        {
            var test = @"
using System;

class Program
{
    public static void Main()
    {
         var task = CreateTask();
         Console.WriteLine(task.Name);
    }

    private static Task CreateTask()
    {
        var t = new Task();
        return t;
    }
}

public class Task
{
    public int Name { get; set; }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public async Task TestMethod1()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class {|#0:TypeName|}
        {   
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

            var expected = VerifyCS.Diagnostic("NonAwaitedAsync").WithLocation(0).WithArguments("TypeName");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}
