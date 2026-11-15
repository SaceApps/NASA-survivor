using System.Text.Json;
using System.Text.Json.Serialization;

namespace MissaoMarte.Nucleo.Dados;

/// <summary>Um fato real, com a origem, o endereço e a data de referência (RF: nenhum dado sem fonte).</summary>
public sealed class FatoNasa
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("categoria")] public string Categoria { get; set; } = "";
    [JsonPropertyName("titulo")] public string Titulo { get; set; } = "";
    [JsonPropertyName("texto")] public string Texto { get; set; } = "";
    [JsonPropertyName("valor")] public string Valor { get; set; } = "";
    [JsonPropertyName("fonte")] public string Fonte { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("data_referencia")] public string DataReferencia { get; set; } = "";
    [JsonPropertyName("confianca")] public string Confianca { get; set; } = "";
    [JsonPropertyName("nota")] public string Nota { get; set; } = "";
}

public sealed class ImagemNasa
{
    [JsonPropertyName("nasa_id")] public string NasaId { get; set; } = "";
    [JsonPropertyName("titulo")] public string Titulo { get; set; } = "";
    [JsonPropertyName("descricao")] public string Descricao { get; set; } = "";
    [JsonPropertyName("data")] public string Data { get; set; } = "";
    [JsonPropertyName("credito")] public string Credito { get; set; } = "";
    [JsonPropertyName("url_thumb")] public string UrlThumb { get; set; } = "";
    [JsonPropertyName("url_pagina")] public string UrlPagina { get; set; } = "";
    [JsonPropertyName("tema")] public string Tema { get; set; } = "";
}

public sealed class ServicoNasa
{
    [JsonPropertyName("nome")] public string Nome { get; set; } = "";
    [JsonPropertyName("url_exemplo")] public string UrlExemplo { get; set; } = "";
    [JsonPropertyName("funciona")] public bool Funciona { get; set; }
    [JsonPropertyName("sem_chave")] public bool SemChave { get; set; }
    [JsonPropertyName("observacao")] public string Observacao { get; set; } = "";
    [JsonPropertyName("data_teste")] public string DataTeste { get; set; } = "";
}

/// <summary>
/// O PACOTE OFFLINE de dados reais: o jogo funciona sem internet com ele. Foi montado em 14/11/2026 abrindo cada
/// fonte; a cópia completa da pesquisa está em docs/pesquisa. Com internet, a tela pode buscar imagens novas.
/// </summary>
public sealed class PacoteNasa
{
    [JsonPropertyName("gerado_em")] public string GeradoEm { get; set; } = "";
    [JsonPropertyName("aviso")] public string Aviso { get; set; } = "";
    [JsonPropertyName("fatos")] public List<FatoNasa> Fatos { get; set; } = new();
    [JsonPropertyName("imagens")] public List<ImagemNasa> Imagens { get; set; } = new();
    [JsonPropertyName("servicos")] public List<ServicoNasa> Servicos { get; set; } = new();

    private static readonly Lazy<PacoteNasa> _padrao = new(Carregar);
    public static PacoteNasa Padrao => _padrao.Value;

    public static PacoteNasa Carregar()
    {
        var asm = typeof(PacoteNasa).Assembly;
        var nome = asm.GetManifestResourceNames().Single(n => n.EndsWith("nasa.json", StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(nome)!;
        return JsonSerializer.Deserialize<PacoteNasa>(s) ?? throw new InvalidOperationException("nasa.json vazio");
    }

    public FatoNasa? Fato(string? id) => id is null ? null : Fatos.FirstOrDefault(f => f.Id == id);
}
