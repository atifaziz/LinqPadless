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
