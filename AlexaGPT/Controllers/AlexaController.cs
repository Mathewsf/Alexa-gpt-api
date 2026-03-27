using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace AlexaGPT.Controllers
{
    [ApiController]
    [Route("alexa")]
    public class AlexaController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public AlexaController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JsonElement body)
        {
            var userText = body
                .GetProperty("request")
                .GetProperty("intent")
                .GetProperty("slots")
                .GetProperty("query")
                .GetProperty("value")
                .GetString();

            var apiKey = _config["OpenAI:ApiKey"];

            var client = _httpClientFactory.CreateClient();

            var openAiRequest = new
            {
                model = "gpt-4.1-mini",
                messages = new[]
                {
                    new { role = "system", content = "Você é um assistente útil." },
                    new { role = "user", content = userText }
                }
            };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(openAiRequest),
                Encoding.UTF8,
                "application/json"
            );

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);

            var responseString = await response.Content.ReadAsStringAsync();

            using var responseDoc = JsonDocument.Parse(responseString);

            var answer = responseDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var alexaResponse = new
            {
                version = "1.0",
                response = new
                {
                    outputSpeech = new
                    {
                        type = "PlainText",
                        text = answer
                    },
                    shouldEndSession = false
                }
            };

            return Ok(alexaResponse);
        }
    }
}