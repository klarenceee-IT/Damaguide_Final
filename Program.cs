using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Retrieve key securely from environment variables or appsettings.json
string openRouterApiKey = builder.Configuration["OPENROUTER_API_KEY"] ?? string.Empty;

app.MapGet("/", () => "Damaguide OpenRouter Vision API is live!");

app.MapPost("/api/damage/analyze", async (HttpRequest request) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(openRouterApiKey))
        {
            return Results.Problem("OPENROUTER_API_KEY is missing or not configured on the server.");
        }

        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("image");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No image file uploaded." });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        byte[] imageBytes = ms.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        string mimeType = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;

        var promptText = @"
            Analyze the image carefully and focus on identifying physical furniture damage to make an answer more accurate.

            1. If the image does NOT contain furniture, set FurnitureType to ""Not Furniture"" and DamageStatus to ""Not Recognizable"".
            2. If the image IS furniture but has NO damage (it is intact/undamaged), set DamageStatus to ""Intact / No Damage"" and DamageSeverity to ""None"".
            3. If the image is damaged furniture, describe the type, location, severity, and realistic repair steps.

            Return ONLY a valid raw JSON object using these exact keys:
            {
            ""FurnitureType"": ""Type of furniture or 'Not Furniture'"",
            ""DamageStatus"": ""Status (e.g. Intact / No Damage, Damaged, Severely Broken, Not Recognizable)"",
            ""DamageLocation"": ""Specific area affected or 'N/A' if intact/unrecognized"",
            ""DamageSeverity"": ""Severity rating (None, Low, Moderate, High, Critical)"",
            ""Confidence"": 90,
            ""RepairRecommendation"": ""Step-by-step repair instructions OR confirmation that no repair is required.""
            }";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openRouterApiKey.Trim());

        var payload = new
        {
            // openrouter/free automatically routes to available free vision models
            model = "openrouter/free", 
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = promptText },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{mimeType};base64,{base64Image}"
                            }
                        }
                    }
                }
            }
        };

        var response = await httpClient.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", payload);
        string responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[OpenRouter Error] Status: {response.StatusCode}\nDetails: {responseString}");
            return Results.Problem($"OpenRouter API error ({response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        string aiText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        // Clean markdown code block markers standard in LLM responses
        aiText = aiText.Replace("```json", "").Replace("```", "").Trim();

        Console.WriteLine($"[Raw AI Output]: {aiText}");

        // Safely parse JSON or fallback to structured wrapper
        if (aiText.StartsWith("{") || aiText.StartsWith("["))
        {
            var parsedJson = JsonSerializer.Deserialize<object>(aiText);
            return Results.Ok(parsedJson);
        }
        else
        {
            return Results.Ok(new
            {
                FurnitureType = "Analyzed Item",
                DamageStatus = "Analyzed",
                DamageLocation = "See Details",
                DamageSeverity = "Moderate",
                Confidence = 85,
                RepairRecommendation = aiText
            });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Server Exception] {ex.Message}\n{ex.StackTrace}");
        return Results.Problem($"Internal Server Exception: {ex.Message}");
    }
});

app.Run();