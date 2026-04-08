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
        public IActionResult Post([FromBody] JsonElement body)
        {
            try
            {
                Console.WriteLine("Request recebida da Alexa");

                //SAFE GET
                if (!body.TryGetProperty("request", out var request))
                    return ErrorResponse("Invalid request");

                string requestType = request.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString() ?? ""
                    : "";

                string locale = request.TryGetProperty("locale", out var localeProp)
                    ? localeProp.GetString() ?? "en-US"
                    : "en-US";

                Console.WriteLine($"Tipo: {requestType} | Locale: {locale}");

                bool isPortuguese = locale.StartsWith("pt");

                // LaunchRequest
                if (requestType == "LaunchRequest")
                {
                    return Response(
                        isPortuguese
                            ? "Olá, sou seu assistente inteligente. Pode perguntar o que quiser."
                            : "Hello, I am your smart assistant. Ask me anything.",
                        false
                    );
                }

                //IntentRequest
                if (requestType == "IntentRequest")
                {
                    string query = "";

                    if (request.TryGetProperty("intent", out var intent) &&
                        intent.TryGetProperty("slots", out var slots) &&
                        slots.TryGetProperty("query", out var querySlot) &&
                        querySlot.TryGetProperty("value", out var value))
                    {
                        query = value.GetString() ?? "";
                    }

                    Console.WriteLine($"Pergunta: {query}");

                    string resposta = isPortuguese
                        ? "Não entendi."
                        : "I didn't understand.";

                    if (!string.IsNullOrEmpty(query))
                    {
                        var apiKey = _config["OpenAI:ApiKey"];

                        if (string.IsNullOrEmpty(apiKey))
                        {
                            Console.WriteLine("API Key não configurada!");
                            resposta = isPortuguese
                                ? "Erro de configuração."
                                : "Configuration error.";
                        }
                        else
                        {
                            var client = _httpClientFactory.CreateClient();

                            string prompt = isPortuguese
                                ? $"Responda em português de forma curta e clara: {query}"
                                : $"Answer briefly and clearly: {query}";

                            var requestBody = new
                            {
                                model = "gpt-4.1-mini",
                                input = prompt
                            };

                            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");

                            requestMessage.Content = new StringContent(
                                JsonSerializer.Serialize(requestBody),
                                Encoding.UTF8,
                                "application/json"
                            );

                            var response = client.SendAsync(requestMessage).Result;
                            var json = response.Content.ReadAsStringAsync().Result;

                            Console.WriteLine("Resposta OpenAI:");
                            Console.WriteLine(json);

                            if (response.IsSuccessStatusCode)
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(json);

                                    if (doc.RootElement.TryGetProperty("output", out var output) &&
                                        output.ValueKind == JsonValueKind.Array &&
                                        output.GetArrayLength() > 0)
                                    {
                                        var first = output[0];

                                        if (first.TryGetProperty("content", out var content) &&
                                            content.ValueKind == JsonValueKind.Array &&
                                            content.GetArrayLength() > 0)
                                        {
                                            var text = content[0].TryGetProperty("text", out var txt)
                                                ? txt.GetString()
                                                : null;

                                            if (!string.IsNullOrEmpty(text))
                                                resposta = text;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Erro parse OpenAI:");
                                    Console.WriteLine(ex);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Erro OpenAI: {response.StatusCode}");
                                resposta = isPortuguese
                                    ? "Erro ao consultar IA."
                                    : "Error contacting AI.";
                            }
                        }
                    }

                    return Response(resposta, false);
                }

                // fallback
                return Response(
                    isPortuguese
                        ? "Desculpe, não consegui processar."
                        : "Sorry, I couldn't process your request.",
                    true
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO GERAL:");
                Console.WriteLine(ex);

                return Response("Erro interno.", true);
            }
        }

        //Helpers corrigidos (retornam IActionResult)
        private IActionResult Response(string text, bool endSession)
        {
            return Ok(new
            {
                version = "1.0",
                response = new
                {
                    outputSpeech = new
                    {
                        type = "PlainText",
                        text = text
                    },
                    shouldEndSession = endSession
                }
            });
        }

        private IActionResult ErrorResponse(string message)
        {
            return Response(message, true);
        }
    }
}