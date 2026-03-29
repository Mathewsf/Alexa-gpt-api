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

                Console.WriteLine($"Tipo da request: {requestType}");

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
                                text = "Hello, I am your smart assistant. Ask me anything."
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

                    Console.WriteLine($"Pergunta recebida: {query}");

                    string resposta = "I didn't understand.";

                    if (!string.IsNullOrEmpty(query))
                    {
                        var apiKey = _config["OpenAI:ApiKey"];

                        if (string.IsNullOrEmpty(apiKey))
                        {
                            Console.WriteLine("API Key não configurada!");
                            resposta = "Configuration error.";
                        }
                        else
                        {
                            var client = _httpClientFactory.CreateClient();

                            var requestBody = new
                            {
                                model = "gpt-4.1-mini",
                                input = query
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
                                resposta = "Error contacting AI service.";
                            }
                            else
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(json);

                                    resposta = doc.RootElement
                                        .GetProperty("output")[0]
                                        .GetProperty("content")[0]
                                        .GetProperty("text")
                                        .GetString() ?? "No response";
                                }
                                catch
                                {
                                    Console.WriteLine("Erro ao parsear resposta OpenAI");
                                    resposta = "Error reading AI response.";
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
                            text = "Sorry, I couldn't process your request."
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
                            text = "Error processing request."
                        },
                        shouldEndSession = true
                    }
                });
            }
        }
    }
}