using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;

namespace PersonaChat.Tests;

public class LlmClientTests
{
    private static readonly LlmRequest Plain = new("Be kind.", [new ChatMessage(ChatRole.User, "Hi"), new ChatMessage(ChatRole.Assistant, "Hello")]);
    private static readonly LlmRequest Json = Plain with { JsonSchema = """{"type":"object"}""", LongReply = true };

    private sealed class FakeHandler(string reply, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public JsonElement Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement;
            return new HttpResponseMessage(status) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
        }
    }

    private static IOptions<LlmOptions> Settings() => Options.Create(new LlmOptions
    {
        OpenAi = { ApiKey = "test-key", Model = "gpt-test" },
        Gemini = { ApiKey = "test-key", Model = "gemini-test" },
        OpenAiCompatible = { Url = "http://localhost:1234/v1/chat/completions", Model = "local" },
    });

    [Fact]
    public async Task Ollama_SendsSchemaAndLongReplyBudget()
    {
        var handler = new FakeHandler("""{"message":{"content":" ok "}}""");
        var client = new OllamaClient(new HttpClient(handler), Options.Create(new LlmOptions()), NullLogger<OllamaClient>.Instance);

        Assert.Equal("ok", await client.SendAsync(Json, default));
        Assert.Equal("object", handler.Body.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(2000, handler.Body.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal("system", handler.Body.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Ollama_OmitsFormatForPlainChat()
    {
        var handler = new FakeHandler("""{"message":{"content":"ok"}}""");
        var client = new OllamaClient(new HttpClient(handler), Options.Create(new LlmOptions()), NullLogger<OllamaClient>.Instance);

        await client.SendAsync(Plain, default);

        Assert.False(handler.Body.TryGetProperty("format", out _));
    }

    [Fact]
    public async Task OpenAi_UsesResponsesApiShape()
    {
        var handler = new FakeHandler("""{"output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"done"}]}]}""");
        var client = new OpenAiClient(new HttpClient(handler), Settings(), NullLogger<OpenAiClient>.Instance);

        Assert.Equal("done", await client.SendAsync(Json, default));
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("Be kind.", handler.Body.GetProperty("instructions").GetString());
        Assert.Equal("json_object", handler.Body.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Compatible_WorksWithoutApiKey()
    {
        var handler = new FakeHandler("""{"choices":[{"message":{"role":"assistant","content":"done"}}]}""");
        var client = new OpenAiCompatibleClient(new HttpClient(handler), Settings(), NullLogger<OpenAiCompatibleClient>.Instance);

        Assert.Equal("done", await client.SendAsync(Plain, default));
        Assert.Null(handler.Request!.Headers.Authorization);
        Assert.Equal("system", handler.Body.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Gemini_UsesModelRoleAndKeyHeader()
    {
        var handler = new FakeHandler("""{"candidates":[{"content":{"parts":[{"text":"do"},{"text":"ne"}]}}]}""");
        var client = new GeminiClient(new HttpClient(handler), Settings(), NullLogger<GeminiClient>.Instance);

        Assert.Equal("done", await client.SendAsync(Json, default));
        Assert.Contains("gemini-test:generateContent", handler.Request!.RequestUri!.ToString());
        Assert.True(handler.Request.Headers.Contains("x-goog-api-key"));
        Assert.Equal("model", handler.Body.GetProperty("contents")[1].GetProperty("role").GetString());
        Assert.Equal("application/json", handler.Body.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
    }

    [Fact]
    public async Task ErrorStatus_BecomesFriendlyException()
    {
        var handler = new FakeHandler("{}", HttpStatusCode.Unauthorized);
        var client = new OpenAiClient(new HttpClient(handler), Settings(), NullLogger<OpenAiClient>.Instance);

        await Assert.ThrowsAsync<LlmException>(() => client.SendAsync(Plain, default));
    }

    [Theory]
    [InlineData(LlmProviders.Ollama, false)]
    [InlineData(LlmProviders.OpenAi, true)]
    [InlineData(LlmProviders.Gemini, true)]
    [InlineData(LlmProviders.Claude, true)]
    [InlineData(LlmProviders.OpenAiCompatible, true)]
    [InlineData("Nonsense", true)]
    public void Validator_RequiresSettingsOnlyForSelectedProvider(string provider, bool shouldFail)
    {
        var result = new LlmOptionsValidator().Validate(null, new LlmOptions { Provider = provider });

        Assert.Equal(shouldFail, result.Failed);
    }
}
