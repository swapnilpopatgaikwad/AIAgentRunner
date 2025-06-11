# 🧠 AIAgentRunner

**AIAgentRunner** is a console-based intelligent agent built with C# that interacts with the [Gemini API](https://ai.google.dev/) to simulate multi-phase AI reasoning: **THINK → ACTION → OBSERVE → OUTPUT**. The assistant follows a deterministic, step-wise format using strict JSON responses and dynamically invokes tools based on user queries.

---

## 🚀 Features

- 🧩 Step-by-step AI interaction cycle
- 🧠 Emulates structured reasoning and tool usage
- 🔧 Supports custom tools (e.g., `getExchangeRate`)
- 💬 Uses [Gemini Flash API](https://ai.google.dev/)
- ✅ Parses and validates clean JSON responses
- 🛠 Easily extensible with more tools

---

## 📦 Example Tool Included

### ✅ `getExchangeRate(currency: string): string`

Handles currency-based queries like:
```
User: What is exchange rate of USD?
AI Flow: THINK → ACTION → OBSERVE → OUTPUT
```

---

## 📸 Sample Output

```
You: What is exchange rate of USD?
Think: The user is asking for exchange rate of USD.
Think: I should use getExchangeRate tool.
Tool Action Called: GetExchangeRate with input USD
tool: GetExchangeRate | input: USD | value: Exchange rate of USD is 83.15 INR
Think: The output of getExchangeRate for USD is 83.15 INR.
Output: The exchange rate of USD is 83.15 INR.
```

---

## 🏗 Project Structure

```
AIAgentRunner/
│
├── Program.cs            # Main AI agent logic
├── JsonResponse.cs       # Model for AI JSON responses
├── StepEnum.cs           # Enumeration of all step types
├── RoleMessage.cs        # Gemini message schema
└── README.md             # Documentation
```

---

## 🔐 Setup

1. **Clone the Repo**
   ```bash
   git clone https://github.com/swapnilpopatgaikwad/AIAgentRunner.git
   cd AIAgentRunner
   ```

2. **Add Your Gemini API Key**
   Replace the `ApiKey` value in `Program.cs`:
   ```csharp
   private static readonly string ApiKey = "YOUR_GEMINI_API_KEY";
   ```

3. **Run the Project**
   - Open in Visual Studio or run via CLI:
     ```bash
     dotnet run
     ```

---

## 🛠️ Dependencies

- [.NET 8+](https://dotnet.microsoft.com/)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)

Install via NuGet:
```bash
dotnet add package Newtonsoft.Json
```

---

## 🧩 Extendable Tool System

Add more tools like this:
```csharp
AvailableTools.Add("GetWeatherInfo", city => $"Weather in {city} is 30°C");
```
