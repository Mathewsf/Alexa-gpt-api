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

                string? requestType = body
                    .GetProperty("request")
                    .GetProperty("type")
                    .GetString();

                string locale = body
                    .GetProperty("request")
                    .GetProperty("locale")
                    .GetString() ?? "en-US";

                Console.WriteLine($"Tipo: {requestType} | Locale: {locale}");

                // 🔹 Detecta idioma
                bool isPortuguese = locale.StartsWith("pt");

                // 🔹 Quando abre a skill
                if (requestType == "LaunchRequest")
                {
                    return Ok(new
                    {
                        version = "1.0",
                        response = new
                        {
                            outputSpeech = new
                            {
                                type = "PlainText",
                                text = isPortuguese
                                    ? "Olá, sou seu assistente inteligente. Pode perguntar o que quiser."
                                    : "Hello, I am your smart assistant. Ask me anything."
                            },
                            shouldEndSession = false
                        }
                    });
                }

                // 🔹 Quando faz pergunta
                if (requestType == "IntentRequest")
                {
                    string query = "";

                    var request = body.GetProperty("request");

                    if (request.TryGetProperty("intent", out var intent))
                    {
                        if (intent.TryGetProperty("slots", out var slots))
                        {
                            if (slots.TryGetProperty("query", out var querySlot))
                            {
                                if (querySlot.TryGetProperty("value", out var value))
                                {
                                    query = value.GetString() ?? "";
                                }
                            }
                        }
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

                            // 🔥 Prompt com idioma + resposta curta
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

                            if (!response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Erro OpenAI: {response.StatusCode}");
                                resposta = isPortuguese
                                    ? "Erro ao consultar a inteligência artificial."
                                    : "Error contacting AI service.";
                            }
                            else
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(json);

                                    // 🔥 PARSE SEGURO (NÃO QUEBRA MAIS)
                                    if (doc.RootElement.TryGetProperty("output", out var output) &&
                                        output.ValueKind == JsonValueKind.Array &&
                                        output.GetArrayLength() > 0)
                                    {
                                        var first = output[0];

                                        if (first.TryGetProperty("content", out var content) &&
                                            content.ValueKind == JsonValueKind.Array &&
                                            content.GetArrayLength() > 0)
                                        {
                                            var text = content[0].GetProperty("text").GetString();

                                            if (!string.IsNullOrEmpty(text))
                                            {
                                                resposta = text;
                                            }
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    Console.WriteLine("Erro ao parsear resposta OpenAI:");
                                    Console.WriteLine(parseEx.ToString());

                                    resposta = isPortuguese
                                        ? "Erro ao interpretar resposta."
                                        : "Error reading AI response.";
                                }
                            }
                        }
                    }

                    return Ok(new
                    {
                        version = "1.0",
                        response = new
                        {
                            outputSpeech = new
                            {
                                type = "PlainText",
                                text = resposta
                            },
                            shouldEndSession = false
                        }
                    });
                }

                // 🔹 fallback
                return Ok(new
                {
                    version = "1.0",
                    response = new
                    {
                        outputSpeech = new
                        {
                            type = "PlainText",
                            text = isPortuguese
                                ? "Desculpe, não consegui processar."
                                : "Sorry, I couldn't process your request."
                        },
                        shouldEndSession = true
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO GERAL:");
                Console.WriteLine(ex.ToString());

                return Ok(new
                {
                    version = "1.0",
                    response = new
                    {
                        outputSpeech = new
                        {
                            type = "PlainText",
                            text = "Erro interno."
                        },
                        shouldEndSession = true
                    }
                });
            }
        }
    }
}