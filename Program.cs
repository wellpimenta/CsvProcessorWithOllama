using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;


var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// AI HttpClient
builder.Services.AddHttpClient("ollama");

// MongoDB
var mongoConn = builder.Configuration.GetSection("Mongo:ConnectionString").Value ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoConn);
builder.Services.AddSingleton(mongoClient);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Ponto final para executar o processamento em todos os arquivos CSV em InputFolder
app.MapPost("/execute", async (HttpContext http, IConfiguration config, MongoClient client) =>
{
    var inputFolder = config["InputFolder"] ?? "./input";
    var outputFolder = config["OutputFolder"] ?? "./output";
    var headersXmlPath = config["HeadersXml"] ?? "./headers.xml";

    Directory.CreateDirectory(inputFolder);
    Directory.CreateDirectory(outputFolder);

    if (!System.IO.File.Exists(headersXmlPath))
    {
        http.Response.StatusCode = 500;
        await http.Response.WriteAsync($"Arquivo headers.XML não encontrados em {headersXmlPath}");
        return;
    }

    var headerMappings = LoadHeaderMappings(headersXmlPath);

    var csvFiles = Directory.GetFiles(inputFolder, "*.csv");
    if (csvFiles.Length == 0)
    {
        await http.Response.WriteAsync("Nenhum arquivo CSV encontrado na pasta de entrada.");
        return;
    }

    // Para simplificar, processe apenas o primeiro CSV encontrado. Remova .First() se quiser vários.
    var file = csvFiles.First();
    var rows = await ReadCsv(file);

    // Mapear colunas por mapeamento de cabeçalho ou por fallback de posição
    // Trataremos a primeira coluna como chave de agrupamento, a terceira coluna (índice 3)
    // como valor a ser somado, a quarta (índice 4) como taxa

    // Crie uma lista de nomes de colunas a partir do mapeamento ou fallback para posições
    string GetColumnName(int pos)
    {        
        if (headerMappings.TryGetValue(pos, out var name)) return name;
        return $"Column{pos}";
    }

    // Analisar linhas dinâmicamente
    var parsedRows = new List<Dictionary<int, string>>();
    foreach (var r in rows)
    {
        // r é o array dos campos 
        var dict = new Dictionary<int, string>();
        for (int i = 0; i < r.Length; i++) dict[i + 1] = r[i];
        parsedRows.Add(dict);
    }

    // Agrupar pela primeira coluna por conta do layout da minha planilha
    var groups = parsedRows.GroupBy(p => p.ContainsKey(1) ? p[1] : "");

    var results = new List<GroupResult>();
    foreach (var g in groups)
    {
        decimal sumThird = 0m;
        decimal sumCalculated = 0m;
        foreach (var row in g)
        {
            // usar a tereceira coluna = 3 para o agrupamento
            if (row.TryGetValue(3, out var thirdValStr) && Decimal.TryParse(thirdValStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var thirdVal))
            {
                sumThird += thirdVal;
            }

            // taxa da quarta coluna por causa do layout da minha planilha 
            if (row.TryGetValue(4, out var rateStr) && Decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rateVal))
            {
                //aplicar taxa a 'thirdVal' se analisado, caso contrário assumir 0
                if (Decimal.TryParse(thirdValStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var tv))
                {
                    sumCalculated += tv * rateVal;
                }
            }
        }
        results.Add(new GroupResult
        {
            Key = g.Key,
            SumThird = sumThird,
            SumCalculated = sumCalculated
        });
    }

    // Gerar grafico de barras
    var chartPath = Path.Combine(outputFolder, $"chart_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
    GenerateBarChart(results, chartPath);

    // Prepare JSON usando as colunas indicadas como campos de JSON.Criaremos um array de objetos com os campos:
    // GroupKey, SumThird, SumCalculated.
    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });

    // Salvar no MongoDB como um registro
    var dbName = config["Mongo:Database"] ?? "CsvProcesses";
    var collectionName = config["Mongo:Collection"] ?? "ProcessResults";
    var database = client.GetDatabase(dbName);
    var collection = database.GetCollection<BsonDocument>(collectionName);

    var doc = new BsonDocument
    {
        { "ID", Guid.NewGuid().ToString() },
        { "DATE", DateTime.UtcNow },
        { "CONTENT", json }
    };
    await collection.InsertOneAsync(doc);

    // salvar arquivo JSON para saída também
    var jsonFile = Path.Combine(outputFolder, $"group_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
    await System.IO.File.WriteAllTextAsync(jsonFile, json);

    await http.Response.WriteAsJsonAsync(new { message = "Processed", chart = Path.GetFileName(chartPath), jsonFile = Path.GetFileName(jsonFile) });
});

// Ponto final para consultar IA (Ollama) usando linguagem natural simples -> tradutor de filtro
app.MapPost("/ai/chat", async (HttpContext http, IConfiguration config, IHttpClientFactory httpFactory) =>
{
    var body = await JsonSerializer.DeserializeAsync<AiRequest>(http.Request.Body);
    var userQuery = body?.Query ?? "";
    var model = config["Ollama:Model"] ?? "ollama-model";
    var endpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";

    // Por segurança: criaremos um prompt que contém uma breve descrição do esquema do conjunto de dados
    // (leremos headers.xml)
    var headersXmlPath = config["HeadersXml"] ?? "./headers.xml";
    var headerMappings = File.Exists(headersXmlPath) ? LoadHeaderMappings(headersXmlPath) : new Dictionary<int, string>();

    var schemaDescription = new StringBuilder();
    schemaDescription.AppendLine("Dataset columns (position -> name):");
    for (int i = 1; i <= 10; i++)
    {
        if (headerMappings.TryGetValue(i, out var name)) schemaDescription.AppendLine($"{i} -> {name}");
        else schemaDescription.AppendLine($"{i} -> Column{i}");
    }

    // Leia o JSON mais recente salvo na pasta de saída para fornecer contexto de dados
    var outputFolder = config["OutputFolder"] ?? "./output";
    Directory.CreateDirectory(outputFolder);
    var latestJson = Directory.GetFiles(outputFolder, "group_*.json").OrderByDescending(f => f).FirstOrDefault();
    string dataSnippet = "";
    if (latestJson != null)
    {
        dataSnippet = System.IO.File.ReadAllText(latestJson);
        // mantenha o snippet razoavelmente pequeno
        if (dataSnippet.Length > 4000) dataSnippet = dataSnippet.Substring(0, 4000);
    }

    // Build prompt da IA
    // ATENÇÃO:
    // Para melhor funcionamento da IA mantenha as intruções basicas em Ingles pois é mais rapido
    var prompt = $@"You are given a dataset of grouped results (JSON) and a schema. Answer the user's query by 
              returning a JSON array of rows (or simple text if request is non-data). Do not invent new fields.

                Schema:
                {schemaDescription}

                Data (truncated):
                {dataSnippet}

                User query: {userQuery}

                Return only JSON or a concise answer. If the user requested a list of keys, 
                return JSON array of keys. If the user requested a filter, return JSON array of matching group objects.
                ";

    // Call Ollama HTTP API - POST /api/chat com o modelo
    var client = httpFactory.CreateClient("ollama");
    client.BaseAddress = new Uri(endpoint);

    var requestObj = new
    {
        model = model,
        prompt = prompt,
        max_tokens = 1024
    };

    var reqJson = JsonSerializer.Serialize(requestObj);
    var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

    try
    {
        var resp = await client.PostAsync("/api/chat", content);
        var respStr = await resp.Content.ReadAsStringAsync();

        // Para simplificar, retorne a resposta gerada
        await http.Response.WriteAsync(respStr);
    }
    catch (Exception ex)
    {
        http.Response.StatusCode = 500;
        await http.Response.WriteAsync($"AI call failed: {ex.Message}");
    }
});


app.MapGet("/time", (HttpContext http) =>
{
    return Results.Json(new { now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
});

app.Run();

static Dictionary<int, string> LoadHeaderMappings(string xmlPath)
{
    var map = new Dictionary<int, string>();
    try
    {
        var x = XDocument.Load(xmlPath);
        foreach (var h in x.Root.Elements("Header"))
        {
            var name = h.Attribute("name")?.Value ?? "";
            if (int.TryParse(h.Attribute("index")?.Value ?? "", out var idx)) map[idx] = name;
        }
    }
    catch { }

    return map;
}

static async Task<List<string[]>> ReadCsv(string path)
{
    var lines = await File.ReadAllLinesAsync(path);
    var rows = new List<string[]>();
    bool isFirst = true;
    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        // naive split - assumes CSV doesn't contain commas in fields
        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
        if (isFirst)
        {
            // skip header row
            isFirst = false; continue;
        }
        rows.Add(parts);
    }

    return rows;
}

static void GenerateBarChart(List<GroupResult> results, string outputPath)
{
    // simple bar chart using System.Drawing
    int width = 800; int height = 600; int margin = 60;
    using var bmp = new Bitmap(width, height);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.White);

    if (results.Count == 0)
    {
        g.DrawString("No data", new System.Drawing.Font("Arial", 24), Brushes.Black, new PointF(10, 10));
        bmp.Save(outputPath, ImageFormat.Png);
        return;
    }

    var maxVal = results.Max(r => r.SumCalculated);
    if (maxVal <= 0) maxVal = results.Max(r => r.SumThird);
    if (maxVal == 0) maxVal = 1;

    int barWidth = (width - 2 * margin) / results.Count - 10;
    for (int i = 0; i < results.Count; i++)
    {
        var r = results[i];
        float x = margin + i * (barWidth + 10);
        var barHeight = (float)((height - 2 * margin) * (double)(r.SumCalculated / maxVal));
        g.FillRectangle(Brushes.SteelBlue, x, height - margin - barHeight, barWidth, barHeight);
        g.DrawString(r.Key, new System.Drawing.Font("Arial", 10), Brushes.Black, new PointF(x, height - margin + 4));
    }

    // axes
    g.DrawLine(Pens.Black, margin, height - margin, width - margin, height - margin);
    g.DrawLine(Pens.Black, margin, margin, margin, height - margin);

    bmp.Save(outputPath, ImageFormat.Png);
}

record AiRequest(string Query);
record GroupResult
{
    public string Key { get; init; }
    public decimal SumThird { get; init; }
    public decimal SumCalculated { get; init; }
}
