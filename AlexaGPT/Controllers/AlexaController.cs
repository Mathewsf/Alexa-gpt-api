using Microsoft.AspNetCore.Mvc;
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
                // 🔹 Pega o tipo da request
                string? requestType = body
                    .GetProperty("request")
                    .GetProperty("type")
                    .GetString();

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
                                text = "Hello, you can ask me anything."
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

                    string resposta = "I didn't understand.";

                    if (!string.IsNullOrEmpty(query))
                    {
                        resposta = "You asked: " + query;
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
            catch
            {
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