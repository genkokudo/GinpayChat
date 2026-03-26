using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using System.Text.Json;

var builder = Kernel.CreateBuilder();

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", optional: true) // ←上書きされる
    .Build();

var azureSection = config.GetSection("AzureOpenAI");
string? deployment = azureSection["Deployment"];
string? endpoint = azureSection["Endpoint"];
string? apiKey = azureSection["ApiKey"];

if (string.IsNullOrWhiteSpace(deployment))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:Deployment' is missing or empty.");
}
if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' is missing or empty.");
}
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:ApiKey' is missing or empty.");
}

builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
var kernel = builder.Build();

ChatCompletionAgent agent =
    new()
    {
        Name = "SK-Agent",
        Instructions = "You are a helpful assistant.",
        Kernel = kernel,
    };

await foreach (AgentResponseItem<ChatMessageContent> response
    in agent.InvokeAsync("Write a haiku about Semantic Kernel."))
{
    Console.WriteLine(response.Message);
}