#pragma warning disable 105
// CS0105: The using directive for 'namespace' appeared previously in this namespace
// https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0105

// {% imports %}

// {% generator %}

static partial class Program
{
    static async System.Threading.Tasks.Task Main()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        await __run__
        (
            #line 1
            // {% source
            Sample((id, score, link) => new { Id = id, Score = score, Link = link })
            // %}
        );
    }
}
