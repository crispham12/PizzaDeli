using System.Text.Json;

namespace PizzaDeli.Services;

public class AiIntegratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _pythonApiUrl = "https://your-ai-api.onrender.com/embed";

    public AiIntegratorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gửi stream ảnh sang Python AI Microservice để nhận Image Embedding Vector.
    /// </summary>
    public async Task<List<float>> GetImageEmbeddingAsync(Stream imageStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        
        // Ensure stream is at the beginning
        if (imageStream.CanSeek)
            imageStream.Position = 0;

        using var streamContent = new StreamContent(imageStream);
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync(_pythonApiUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"AI Service Error: {err}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<EmbeddingResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (result != null && !string.IsNullOrEmpty(result.Error))
            throw new Exception($"AI Microservice Error: {result.Error}");

        if (result == null || result.Embedding == null || result.Embedding.Count == 0)
            throw new Exception($"Quá trình trích xuất đặc trưng thất bại. API trả về: {json}");

        return result.Embedding;
    }

    /// <summary>
    /// So sánh độ tương đồng (Cosine Similarity) giữa vector ảnh upload (vector A) 
    /// và vector sản phẩm (vector B). 
    /// (Khi vector đã Normalize, Cosine Similarity = Dot Product).
    /// </summary>
    public double CalculateSimilarity(List<float> vecA, List<float> vecB)
    {
        if (vecA == null || vecB == null || vecA.Count != vecB.Count)
            return 0;

        double dotProduct = 0;
        for (int i = 0; i < vecA.Count; i++)
        {
            dotProduct += vecA[i] * vecB[i];
        }
        return dotProduct;
    }

    private class EmbeddingResponse
    {
        public List<float> Embedding { get; set; } = new List<float>();
        public string? Error { get; set; }
    }
}
