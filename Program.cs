using Newtonsoft.Json;
using System.Text;

namespace AIAgentRunner
{
    public class JsonResponse
    {
        public string Step { get; set; }
        public string Content { get; set; }
        public string Tool { get; set; }
        public string Input { get; set; }

        public StepEnum StepType => string.IsNullOrEmpty(Step) ? StepEnum.None :
            Step.ToLower() switch
            {
                "think" => StepEnum.Think,
                "action" => StepEnum.Action,
                "observe" => StepEnum.Observe,
                "output" => StepEnum.Output,
                _ => StepEnum.None
            };
    }

    public enum StepEnum
    {
        None,
        Think,
        Action,
        Observe,
        Output,
    }

    public class Part
    {
        public string text { get; set; }
    }

    public class RoleMessage
    {
        public string role { get; set; }
        public Part[] parts { get; set; }
    }

    internal class Program
    {
        private static readonly string ApiKey = "";
        private static readonly string GeminiApiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={ApiKey}";

        public static Dictionary<string, Func<string, string>> AvailableTools = new(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(GetExchangeRate), GetExchangeRate }
        };

        private static string SYSTEM_PROMPT = $@"
You are a helpful AI Assistant designed to resolve user queries in 5 phases: START, THINK, ACTION, OBSERVE, and OUTPUT.

Steps:
- START: User asks a query.
- THINK: You analyze how to resolve it (at least 3 logical thoughts).
- ACTION: If needed, call an available tool.
- OBSERVE: Wait for tool response.
- OUTPUT: Provide the final result to the user.

Available Tools:
- getExchangeRate(currency: string): string

Rules:
- Use only available tools listed.
- Output exactly one step in strict JSON per message.
- Don't skip steps. Always follow the loop.

Example Flow:
START: What is exchange rate of USD?
THINK: The user is asking for exchange rate of USD.
THINK: I should use getExchangeRate tool.
ACTION: Call tool getExchangeRate with input USD
OBSERVE: Exchange rate of USD is 83.15 INR
THINK: The output of getExchangeRate for USD is 83.15 INR.
OUTPUT: The exchange rate of USD is 83.15 INR.

Output Format:
{{ ""step"": ""string"", ""tool"": ""string"", ""input"": ""string"", ""content"": ""string"" }}";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Gemini AI Chatbot (type 'exit' to quit)");

            var messages = new List<RoleMessage>();

            while (true)
            {
                Console.Write("You: ");
                string userInput = Console.ReadLine();
                if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase)) break;

                messages.Clear();
                messages.Add(new RoleMessage
                {
                    role = "user",
                    parts = new[] { new Part { text = SYSTEM_PROMPT } }
                });

                messages.Add(new RoleMessage
                {
                    role = "user",
                    parts = new[] { new Part { text = userInput } }
                });

                while (true)
                {
                    string botReply = await CallGeminiApiAsync(messages);
                    string cleaned = botReply
                        .Replace("```json", "")
                        .Replace("```", "")
                        .Trim();

                    var jsonResponse = TryParseJson(cleaned);
                    if (jsonResponse == null)
                    {
                        Console.WriteLine($"Invalid response: {cleaned}");
                        break;
                    }

                    messages.Add(new RoleMessage
                    {
                        role = "user",
                        parts = new[] { new Part { text = cleaned } }
                    });

                    if(jsonResponse.StepType== StepEnum.Think)
                    {
                        Console.WriteLine($"Think: {jsonResponse.Content}");
                        continue;
                    }
                    if (jsonResponse.StepType == StepEnum.Output)
                    {
                        Console.WriteLine($"Output: {jsonResponse.Content}");
                        break;
                    }

                    if (jsonResponse.StepType == StepEnum.Action)
                    {
                        Console.WriteLine($"Tool Action Called: {jsonResponse.Tool} with input {jsonResponse.Input}");
                        if (AvailableTools.TryGetValue(jsonResponse.Tool, out var toolFunc))
                        {
                            var value = toolFunc(jsonResponse.Input);
                            Console.WriteLine($"tool: {jsonResponse.Tool} | input: {jsonResponse.Input} | value: {value}");

                            messages.Add(new RoleMessage
                            {
                                role = "user",
                                parts = new[] { new Part { text = JsonConvert.SerializeObject(new JsonResponse { Step = "observe", Content = value }) } }
                            });
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Tool {jsonResponse.Tool} not found!");
                            break;
                        }
                    }
                }
            }
        }

        private static JsonResponse? TryParseJson(string cleaned)
        {
            try
            {
                return JsonConvert.DeserializeObject<JsonResponse>(cleaned);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON Parse Error: {ex.Message}");
                return null;
            }
        }

        private static async Task<string> CallGeminiApiAsync(List<RoleMessage> messages)
        {
            using var httpClient = new HttpClient();
            var requestBody = new { contents = messages };
            string json = System.Text.Json.JsonSerializer.Serialize(requestBody);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(GeminiApiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }
                else if (root.TryGetProperty("error", out var error) &&
                         error.TryGetProperty("message", out var message))
                {
                    return $"API Error: {message.GetString()}";
                }

                return "No valid response from Gemini API.";
            }
            catch (HttpRequestException e)
            {
                return $"Network/API Error: {e.Message}";
            }
            catch (System.Text.Json.JsonException ex)
            {
                return $"JSON Parse Error: {ex.Message}";
            }
        }

        // TOOL: Currency Exchange Example
        public static string GetExchangeRate(string currency)
        {
            return currency.ToUpper() switch
            {
                "USD" => "Exchange rate of USD is 83.15 INR",
                "EUR" => "Exchange rate of EUR is 89.65 INR",
                "INR" => "INR is base currency. Value is 1",
                _ => $"Exchange rate for {currency} is not available."
            };
        }
    }
}
