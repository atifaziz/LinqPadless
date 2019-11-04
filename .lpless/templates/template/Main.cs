#if !LPLESS
#define LINQPAD_STATEMENTS
#define LPLESS_TEMPLATE_V1
#endif

#if !LPLESS_TEMPLATE_V1
#error Incompatible template format.
#endif

#if LINQPAD_PROGRAM_STATIC
#define STATIC
#endif
#if LINQPAD_PROGRAM_TASK || LINQPAD_STATEMENTS || LINQPAD_EXPRESSION
#define TASK
#endif
#if LINQPAD_PROGRAM_VOID || LINQPAD_STATEMENTS || LINQPAD_EXPRESSION
#define VOID
#endif
#if LINQPAD_PROGRAM_ARGS
#define ARGS
#endif

// {% imports
#if TASK
using System.Threading.Tasks;
#endif
// %}

// {% generator %}

partial class UserQuery
{
    public const string PATH =
        // {% path-string
        null
        // %}
        ;
    public const string SOURCE =
        // {% source-string
        null
        // %}
        ;
}

partial class UserQuery
{
    static async System.Threading.Tasks.Task<int> Main(string[] args)
    {
#if !VOID
        return
#endif
#if TASK
            await
#endif
#if !STATIC
            new UserQuery().
#endif
                RunUserAuthoredQuery
#if ARGS
                (args)
#else
                ()
#endif
                ;
#if VOID
        return
#if TASK
            await System.Threading.Tasks.Task.FromResult(0);
#else
            0;
#endif
#endif // VOID
    }
}

#if LINQPAD_PROGRAM

partial class UserQuery
{
    // {% program
#if STATIC && !TASK && VOID && !ARGS
     static void RunUserAuthoredQuery() =>
#elif STATIC && !TASK && !VOID && !ARGS
     static int RunUserAuthoredQuery() =>
#elif STATIC && !TASK && VOID && ARGS
     static void RunUserAuthoredQuery(string[] args) =>
#elif STATIC && !TASK && !VOID && ARGS
     static int RunUserAuthoredQuery(string[] args) =>
#elif STATIC && TASK && VOID && !ARGS
     static async Task RunUserAuthoredQuery() =>
#elif STATIC && TASK && !VOID && !ARGS
     static async Task<int> RunUserAuthoredQuery() =>
#elif STATIC && TASK && VOID && ARGS
     static async Task RunUserAuthoredQuery(string[] args) =>
#elif STATIC && TASK && !VOID && ARGS
     static async Task<int> RunUserAuthoredQuery(string[] args) =>
#elif !STATIC && !TASK && VOID && !ARGS
     void RunUserAuthoredQuery() =>
#elif !STATIC && !TASK && !VOID && !ARGS
     int RunUserAuthoredQuery() =>
#elif !STATIC && !TASK && VOID && ARGS
     void RunUserAuthoredQuery(string[] args) =>
#elif !STATIC && !TASK && !VOID && ARGS
     int RunUserAuthoredQuery(string[] args) =>
#elif !STATIC && TASK && VOID && !ARGS
     async Task RunUserAuthoredQuery() =>
#elif !STATIC && TASK && !VOID && !ARGS
     async Task<int> RunUserAuthoredQuery() =>
#elif !STATIC && TASK && VOID && ARGS
     async Task RunUserAuthoredQuery(string[] args) =>
#elif !STATIC && TASK && !VOID && ARGS
     async Task<int> RunUserAuthoredQuery(string[] args) =>
#endif
        throw new NotImplementedException();
     // %}
}
// {% program-types %}
// {% program-namespaces %}

#elif LINQPAD_EXPRESSION

partial class UserQuery
{
    async System.Threading.Tasks.Task RunUserAuthoredQuery()
    {
        System.Console.WriteLine(
            // {% expression %}
        );
    }
}

#elif LINQPAD_STATEMENTS

partial class UserQuery
{
    async System.Threading.Tasks.Task RunUserAuthoredQuery()
    {
        // {% statements %}
    }
}

#else
#error Unsupported typeof LINQPad query
#endif
