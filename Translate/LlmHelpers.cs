using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Translate;

public static class LlmHelpers
{
    public static object GenerateSystemPrompt(string? systemPrompt)
    {
        return new { role = "system", content = systemPrompt };
    }

    public static object GenerateUserPrompt(string? text)
    {
        return new { role = "user", content = text };
    }

    public static object GenerateAssistantPrompt(string? text)
    {
        return new { role = "assistant", content = text };
    }

    public static string GenerateLlmRequestData(LlmConfig config, List<object> messages)
    {
        if (config.ModelParams != null)
        {
            // Create a dynamic object and populate it with Params
            dynamic requestBody = new ExpandoObject();
            requestBody.model = config.Model;
            requestBody.stream = false;
            requestBody.messages = messages;

            // Add each key-value pair from Params to the dynamic object
            var requestBodyDict = (IDictionary<string, object>)requestBody;
            foreach (var param in config.ModelParams)
                requestBodyDict[param.Key] = param.Value;

            return JsonSerializer.Serialize(requestBody);
        }
        else
        {
            var requestBody = new
            {
                model = config.Model,
                temperature = 0.1,
                max_tokens = 1000,
                top_p = 1.0,
                top_k = 20,
                min_p = 0.05,
                frequency_penalty = 0,
                presence_penalty = 0,
                stream = false,
                messages
            };

            return JsonSerializer.Serialize(requestBody);
        }
    }
}