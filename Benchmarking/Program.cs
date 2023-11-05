using NBomber.CSharp;
using NBomber.Http;
using HttpVersion = NBomber.Http.HttpVersion;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using HtmlAgilityPack;
using NBomber.Contracts;
using NBomber.Http.CSharp;

// using NUglify;
// using NUglify.Html;
// using HtmlNode = NUglify.Html.HtmlNode;

var htmlContent = """<header class="mb32 pt12"><time datetime=2023-09-07T17:30:40.673Z class="flex--item fc-black-700 tt-uppercase fw-bold fs-body1 fc-black-700 d-flex mb16" itemprop=datePublished><svg aria-hidden=true class="svg-icon iconCalendar va-middle mr6 mtn1" width=18 height=18 viewbox="0 0 18 18"><path d="M14 2h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V4c0-1.1.9-2 2-2h1V0h2v2h6V0h2v2ZM3 6v9h12V6H3Zm2 2h2v2H5V8Zm0 3h2v2H5v-2Zm3 0h2v2H8v-2Zm3 0h2v2h-2v-2Zm0-3h2v2h-2V8ZM8 8h2v2H8V8Z"></path></svg> September 7, 2023</time><h1 class="fs-display2 lh-xs p-ff-roboto-slab-bold mb24" itemprop=name>Computers are learning to read our minds</h1><p class="fs-title fc-black-500 wmx6" itemprop=abstract>The home team chats with Gašper Beguš, director of the Berkeley Speech and Computation Lab, about his research into how LLMs—and humans—learn to speak. Plus: how AI is restoring a stroke survivor’s ability to talk, concern over models that pass the Turing test, and what’s going on with whale brains.</p><img src="https://cdn.stackoverflow.co/images/jo7n4k8s/production/56c6dab9c7d1b66bc662c5f43ea9573418d94d17-2400x1260.webp?w=1200&amp;h=630&amp;auto=format&amp;dpr=2" width=1200 height=630 class="bar-md w100 mt32 h-auto d-block as-start ba bc-black-050" alt="Article hero image"></header><div itemprop=articleBody class="s-prose fs-subheading"><div class="p-slice-embed overflow-hidden bar-lg bs-sm" data-v-5a887ead=""><div style=padding-top:200px class="w100 h0 ps-relative bg-black-025" data-v-5a887ead=""><iframe class="w100 h100 t0 r0 b0 l0 ps-absolute" width=200 height=120 src=https://player.simplecast.com/d57b1e6e-e7f3-416d-9d9f-df3e40ecfc56 frameborder=0 allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen="" data-v-5a887ead=""></iframe></div></div><p><a href=https://vcresearch.berkeley.edu/faculty/gasper-begus>Gašper</a>’s work combines machine learning, statistical modeling, neuroimaging, and behavioral experiments “to better understand how neural networks learn internal representations in speech and how humans learn to speak.”<p>One thing that surprised him about <a href=https://developers.google.com/machine-learning/gan/gan_structure>generative adversarial networks (GANs)</a>? How innovative they are, capable of generating English words they’ve never heard before based on words they have.<p>Read about <a href=https://www.nytimes.com/2023/08/23/health/ai-stroke-speech-neuroscience.html>how AI is restoring a stroke survivor’s ability to speak</a>.<p><a href=https://en.wikipedia.org/wiki/Universal_grammar>Universal grammar</a> proposes a hypothetical structure in the brain responsible for humans’ innate language abilities. The concept is credited to the famous linguist Noam Chomsky; read <a href=https://www.nytimes.com/2023/03/08/opinion/noam-chomsky-chatgpt-ai.html>his take on GenAI</a>.<p>AI expert <a href=https://yoshuabengio.org/>Yoshua Bengio</a> recently signed an <a href=https://futureoflife.org/open-letter/pause-giant-ai-experiments/>open letter</a> asking AI labs to pause the training of AI systems powerful enough to pass the <a href=https://www.techtarget.com/searchenterpriseai/definition/Turing-test>Turing test</a>. Read about <a href=https://yoshuabengio.org/2023/04/05/slowing-down-development-of-ai-systems-passing-the-turing-test/>his reasoning</a>.<p>Find the Berkeley Speech and Communication Network <a href=https://twitter.com/berkeleysclab>here</a>.<p>Find Gašper on <a href=https://gbegus.github.io/>his website</a>, <a href="https://twitter.com/begusgasper?ref_src=twsrc%5Egoogle%7Ctwcamp%5Eserp%7Ctwgr%5Eauthor">Twitter</a>, and <a href=https://www.linkedin.com/in/gbegus/>LinkedIn</a>. Or dive into <a href="https://scholar.google.si/citations?user=r7gAWagAAAAJ&amp;hl=sl">his research</a>.<p>Congratulations to <a href=https://stackoverflow.com/help/badges/8842/lifeboat>Lifeboat badge</a> winner and self-proclaimed data nerd <a href=https://stackoverflow.com/users/174777/john-rotenstein>John Rotenstein</a>, who saved <a href=https://stackoverflow.com/questions/50467698/how-can-i-delete-files-older-than-seven-days-in-amazon-s3>How can I delete files older than seven days in Amazon S3?</a> from the ignominy of ignorance.<p><a href=https://the-stack-overflow-podcast.simplecast.com/episodes/computers-are-learning-to-read-our-minds/transcript>TRANSCRIPT</a><p><p></div>""";
// var UglifyResults = new List<dynamic>
// {
//     Uglify.HtmlToText(htmlContent, HtmlToTextOptions.None, null),
//     Uglify.HtmlToText(htmlContent, HtmlToTextOptions.KeepStructure, null),
//     Uglify.HtmlToText(htmlContent, HtmlToTextOptions.KeepFormatting, null),
//     Uglify.HtmlToText(htmlContent, HtmlToTextOptions.KeepHtmlEscape, null),
//     Uglify.HtmlToText(htmlContent, HtmlToTextOptions.KeepStructure | HtmlToTextOptions.KeepFormatting | HtmlToTextOptions.KeepHtmlEscape, null),
// };

// Setup the configuration to support document loading
var config = Configuration.Default;
var context = BrowsingContext.New(config);

// Load the document from the string
var document = await context.OpenAsync(req => req.Content(htmlContent));

// Extract the text content
var textContent1 = document.DocumentElement.TextContent;

// Load the HTML content into a document
var doc = new HtmlDocument();
doc.LoadHtml(htmlContent);
var node = doc.DocumentNode;
var textContent2 = GetTextWithSpaces(node);

// Extract the text content
static string GetTextWithSpaces(HtmlNode node)
{
    var text = new StringBuilder();
    foreach (var child in node.ChildNodes)
    {
        if (child.NodeType == HtmlNodeType.Text)
        {
            // Append the text content directly
            text.Append(child.InnerText);
        }
        else if (child.NodeType == HtmlNodeType.Element)
        {
            // Append a space before the element's text (if it's not the first child)
            if (child != node.FirstChild)
            {
                text.Append(" ");
            }

            // Recursively append the child's text
            text.Append(GetTextWithSpaces(child));

            // Append a space after the element's text (if it's not the last child)
            if (child != node.LastChild)
            {
                text.Append(" ");
            }
        }
    }

    return text.ToString();
}

static string CleanupText(string text)
{
    // Remove extra spaces around punctuation
    text = Regex.Replace(text, @"\s+([,.!?;:])", "$1");

    // Replace multiple whitespaces (including new lines and tabs) with a single space
    text = Regex.Replace(text, @"\s+", " ");

    // Additional cleanup rules can be added here if needed

    return text.Trim();
}

// Post-process: Regex can be used for more sophisticated formatting if needed
textContent2 = CleanupText(textContent2);

var httpClientHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    // UseProxy = false,
    // MaxConnectionsPerServer = 1,
};

var httpClient = new HttpClient(httpClientHandler)
{
    BaseAddress = new Uri("https://127.0.0.1:6002/api/payment/health", UriKind.RelativeOrAbsolute), Timeout = TimeSpan.FromMinutes(5), DefaultRequestVersion = new Version(2, 0), DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
    // DefaultRequestHeaders =
    // {
    //     // Connection = { "keep-alive" },
    //     // ConnectionClose = true,
    // }
};

var scenario1 = Scenario.Create("server_sent_scenario",
        async context =>
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(15)); // adjust as needed

            var clientArgs = new HttpClientArgs(HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                Version = new Version(2, 0),
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Headers =
                {
                    {
                        "Accept", "text/event-stream"
                    }
                }
            };

            Response<HttpResponseMessage>? response = null;
            try
            {
                response = await Http.Send(httpClient, clientArgs, request);
                await using var stream = await response.Payload.Value.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var buffer = new char[1024];
                while (!cts.IsCancellationRequested && !reader.EndOfStream)
                {
                    await reader.ReadAsync(buffer, 0, buffer.Length);
                }

                await stream.DisposeAsync();
            }
            catch (Exception e)
            {
                if (!cts.IsCancellationRequested)
                {
                    Console.WriteLine("cts.IsCancellationRequested: " + cts.IsCancellationRequested);
                    Console.WriteLine(e);
                    throw;
                }
            }
            finally
            {
                response?.Payload.Value.Content.Dispose();
                response?.Payload.Value.Dispose();
            }
            return response;
        })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.RampingConstant(500, TimeSpan.FromSeconds(30)),
        Simulation.RampingConstant(500, TimeSpan.FromSeconds(15)),
        Simulation.RampingConstant(0, TimeSpan.FromSeconds(10)));

NBomberRunner
    .RegisterScenarios(scenario1)
    .WithWorkerPlugins(new HttpMetricsPlugin(new[]
    {
        HttpVersion.Version1, HttpVersion.Version2, HttpVersion.Version3
    }))
    .Run();

// var scenarioSimple = Scenario.Create("api_scenario", async context =>
//     {
//         var test1 = Http.CreateRequest("GET", "http://127.0.0.1:6002/api/payment?id=PaymentEvents%2F97-A");
//         return await Http.Send(httpClient, test1);
//     })
//     .WithoutWarmUp()
//     .WithLoadSimulations(
//         Simulation.RampingConstant(200, TimeSpan.FromSeconds(10)),
//         Simulation.RampingConstant(200, TimeSpan.FromSeconds(20))
//     );
// NBomberRunner
//     .RegisterScenarios(scenarioSimple)
//     .WithWorkerPlugins(new HttpMetricsPlugin(new[] { HttpVersion.Version1 }))
//     .Run();
