using System.Text.Json;

namespace MissaoMarte.Web.Servicos;

public sealed record ImagemOnline(string NasaId, string Titulo, string Data, string Credito, string Thumb, string Pagina);

/// <summary>
/// Busca opcional de imagens na NASA Image and Video Library (serviço público, sem chave). O jogo não depende dela:
/// sem internet, a tela usa o pacote offline. Só o termo da busca viaja para fora; nenhum dado dos jogadores.
/// </summary>
public sealed class NasaOnline(HttpClient http)
{
    public async Task<(List<ImagemOnline> Imagens, string? Erro)> Buscar(string termo, int quantas = 8)
    {
        try
        {
            var url = $"https://images-api.nasa.gov/search?q={Uri.EscapeDataString(termo)}&media_type=image&page_size={quantas}";
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return (new(), $"A NASA respondeu {(int)resp.StatusCode}.");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var lista = new List<ImagemOnline>();
            foreach (var item in doc.RootElement.GetProperty("collection").GetProperty("items").EnumerateArray())
            {
                if (!item.TryGetProperty("data", out var data) || data.GetArrayLength() == 0) continue;
                var d = data[0];
                string id = Texto(d, "nasa_id");
                string thumb = "";
                if (item.TryGetProperty("links", out var links))
                    foreach (var l in links.EnumerateArray())
                        if (Texto(l, "rel") == "preview") { thumb = Texto(l, "href"); break; }
                if (id == "" || thumb == "") continue;
                string credito = Texto(d, "secondary_creator");
                if (credito == "") credito = Texto(d, "photographer");
                if (credito == "") credito = "NASA" + (Texto(d, "center") is { Length: > 0 } c ? "/" + c : "");
                string data10 = Texto(d, "date_created");
                lista.Add(new ImagemOnline(id, Texto(d, "title"), data10.Length >= 10 ? data10[..10] : data10, credito,
                    thumb.Replace("http://", "https://"), $"https://images.nasa.gov/details/{Uri.EscapeDataString(id)}"));
                if (lista.Count >= quantas) break;
            }
            return (lista, lista.Count == 0 ? "A busca não trouxe imagens." : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return (new(), "Sem conexão com a NASA agora. O pacote offline continua valendo.");
        }
    }

    private static string Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
