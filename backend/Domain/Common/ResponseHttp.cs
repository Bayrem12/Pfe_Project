using System.Text.Json.Serialization;

namespace Application.Setting
{
    /// <summary>
    /// Enveloppe de réponse HTTP standard.
    /// Les attributs [JsonPropertyName] garantissent un JSON camelCase cohérent
    /// même lorsque JsonNamingPolicy.CamelCase est activé globalement.
    /// </summary>
    public class ResponseHttp
    {
        [JsonPropertyName("resultat")]
        public object? Resultat { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("failMessages")]
        public string? FailMessages { get; set; }
    }

    /// <summary>
    /// Variante générique typée — préférer ResponseHttp&lt;T&gt; pour les nouveaux endpoints.
    /// Évite la perte de type et améliore la documentation Swagger.
    /// </summary>
    public class ResponseHttp<T> : ResponseHttp
    {
        [JsonPropertyName("resultat")]
        public new T? Resultat { get; set; }
    }
}
