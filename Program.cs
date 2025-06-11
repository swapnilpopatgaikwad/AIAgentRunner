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

        public static Dictionary<string, Func<string, Task<string>>> AvailableTools = new(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(GetExchangeRate),  input => Task.FromResult(GetExchangeRate(input)) },
            { nameof(RunCmdCommand), RunCmdCommand }
        };

        private static string SYSTEM_PROMPT = $@"
You are a helpful AI Assistant who is designed to resolve user queries.
You work in START, THINK, ACTION, OBSERVE, and OUTPUT modes.

In the START phase, the user gives a query to you.
Then, you THINK how to resolve that query at least 3–4 times.
If there is a need to call a tool, you call an ACTION event with the tool name and input.
If there is an ACTION call, wait for the OBSERVE step, which is the output of the tool.
Based on the OBSERVE from the previous step, you either produce an OUTPUT or repeat the loop.

Rules:
- Always wait for the next step before proceeding.
- Always output a single step and wait for the next step.
- Output must be strictly in JSON format.
- Only call tool actions from the Available Tools list.
- Strictly follow the output format in JSON.

Available Tools:
- getExchangeRate(currencyCode: string): Returns the current exchange rate of USD to the given currency.
- runCmdCommand(command: string): Executes any Windows CMD command. You can use this tool to create files, folders, navigate directories, and perform any other shell operations. It returns the command's output or error.

Example:
START: What is the exchange rate of USD to INR?
THINK: The user is asking for the exchange rate of USD to INR.
THINK: From the available tools, I must call getExchangeRate with INR as input.
ACTION: Call Tool getExchangeRate(INR)
OBSERVE: 1 USD = 83.15 INR
THINK: The output of getExchangeRate for INR is 1 USD = 83.15 INR
OUTPUT: Hey, the current exchange rate of USD to INR is 1 USD = 83.15 INR

Output Example:
{{ ""role"": ""user"", ""content"": ""What is the exchange rate of USD to INR?"" }}
{{ ""step"": ""think"", ""content"": ""The user is asking for the exchange rate of USD to INR."" }}
{{ ""step"": ""think"", ""content"": ""From the available tools, I must call getExchangeRate for INR as input."" }}
{{ ""step"": ""action"", ""tool"": ""getExchangeRate"", ""input"": ""INR"" }}
{{ ""step"": ""observe"", ""content"": ""1 USD = 83.15 INR"" }}
{{ ""step"": ""think"", ""content"": ""The output of getExchangeRate for INR is 1 USD = 83.15 INR"" }}
{{ ""step"": ""output"", ""content"": ""Hey, the current exchange rate of USD to INR is 1 USD = 83.15 INR"" }}

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

                    messages.Add(new RoleMessage
                    {
                        role = "user",
                        parts = new[] { new Part { text = cleaned } }
                    });


                    var jsonResponse = TryParseJson(cleaned);
                    if (jsonResponse == null)
                    {
                        Console.WriteLine($"Invalid response: {cleaned}");
                        break;
                    }
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
                            var value = await toolFunc(jsonResponse.Input);
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

        public static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
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

        // TOOL: CMD Command Executor
        public static async Task<string> RunCmdCommand(string input)
        {
            try
            {
                var projectDirectory = GetProjectRootPath();

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {input}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectDirectory
                };

                using var process = new System.Diagnostics.Process { StartInfo = psi };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(error))
                    return $"[ERROR]\n{error.Trim()}";

                return $"[OUTPUT]\n{output.Trim()}";
            }
            catch (Exception ex)
            {
                return $"[EXCEPTION]\n{ex.Message}";
            }
        }
    }
}
