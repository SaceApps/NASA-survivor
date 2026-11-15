using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MissaoMarte.Nucleo.Simulacao;

/// <summary>Uma linha do catálogo de parâmetros: o valor e de onde ele veio (origem, referência e confiança).</summary>
public sealed class ParametroRef
{
    [JsonPropertyName("chave")] public string Chave { get; set; } = "";
    [JsonPropertyName("valor")] public double Valor { get; set; }
    [JsonPropertyName("unidade")] public string Unidade { get; set; } = "";
    /// <summary>"ciencia" = constante ou medida publicada; "cenario" = escolha de projeto desta base; "referencia" = só informativo.</summary>
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "cenario";
    [JsonPropertyName("descricao")] public string Descricao { get; set; } = "";
    [JsonPropertyName("fonte")] public string Fonte { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("confianca")] public string Confianca { get; set; } = "baixa";
    [JsonPropertyName("nota")] public string? Nota { get; set; }
}

public sealed class CatalogoParametros
{
    [JsonPropertyName("versao_modelo")] public string VersaoModelo { get; set; } = "0";
    [JsonPropertyName("cenario")] public string Cenario { get; set; } = "";
    [JsonPropertyName("nota")] public string Nota { get; set; } = "";
    [JsonPropertyName("parametros")] public List<ParametroRef> Parametros { get; set; } = new();

    public static CatalogoParametros Carregar()
    {
        var asm = typeof(CatalogoParametros).Assembly;
        var nome = asm.GetManifestResourceNames().Single(n => n.EndsWith("parametros.json", StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(nome)!;
        return JsonSerializer.Deserialize<CatalogoParametros>(s) ?? throw new InvalidOperationException("parametros.json vazio");
    }
}

/// <summary>
/// O CENÁRIO DE ENGENHARIA: todos os números que o motor usa. Nenhum valor mora no código: vêm do catálogo
/// (Dados/parametros.json), onde cada um tem unidade, fonte e confiança. Faltou um, o jogo não começa.
/// </summary>
public sealed class ParametrosCenario
{
    public string VersaoModelo { get; private set; } = "0";

    // Marte e o local
    public double SolSegundos { get; set; }
    public double AnoMarcianoSols { get; set; }
    public double SemiEixoUa { get; set; }
    public double Excentricidade { get; set; }
    public double ObliquidadeGraus { get; set; }
    public double LsPerielioGraus { get; set; }
    public double ConstanteSolarTerraWM2 { get; set; }
    public double LatitudeGraus { get; set; }
    public double LsInicialGraus { get; set; }
    public double TempExternaMinC { get; set; }
    public double TempExternaMaxC { get; set; }
    public double TauCeuLimpo { get; set; }
    public double KAtenuacaoPoeira { get; set; }
    public double DeposicaoPoeiraFracaoPorSol { get; set; }

    // Energia
    public double AreaPaineisM2 { get; set; }
    public double EficienciaPainel { get; set; }
    public double BateriaCapacidadeWh { get; set; }
    public double BateriaInicialFracao { get; set; }
    public double BateriaEficienciaCarga { get; set; }
    public double BateriaEficienciaDescarga { get; set; }
    public double BateriaPotenciaMaxW { get; set; }
    public double PerdaDistribuicao { get; set; }

    // Ar
    public double VolumeCabineM3 { get; set; }
    public double PressaoCabineKpa { get; set; }
    public double FracaoO2Cabine { get; set; }
    public double Ppco2InicialKpa { get; set; }
    public double UmidadeInicial { get; set; }
    public double TemperaturaAlvoC { get; set; }
    public double Ppo2AlvoKpa { get; set; }
    public double UmidadeAlvo { get; set; }
    public double VazamentoNominalFracaoDia { get; set; }
    public double TanqueO2InicialKg { get; set; }
    public double TanqueN2InicialKg { get; set; }
    public double Ppo2AlertaKpa { get; set; }
    public double Ppo2PerigoKpa { get; set; }
    public double Ppo2CriticoKpa { get; set; }
    public double Ppco2AlertaKpa { get; set; }
    public double Ppco2PerigoKpa { get; set; }
    public double Ppco2CriticoKpa { get; set; }
    public double PressaoCriticaKpa { get; set; }

    // Metabolismo da tripulação (por tripulante por dia terrestre)
    public double O2ConsumoKgCmDia { get; set; }
    public double Co2ProducaoKgCmDia { get; set; }
    public double AguaBebidaKgCmDia { get; set; }
    public double AguaPreparoAlimentoKgCmDia { get; set; }
    public double AguaNoAlimentoKgCmDia { get; set; }
    public double AguaMetabolicaKgCmDia { get; set; }
    public double UrinaKgCmDia { get; set; }
    public double VaporRespiracaoTranspiracaoKgCmDia { get; set; }
    public double AguaFecalKgCmDia { get; set; }
    public double ResiduoSolidoSecoKgCmDia { get; set; }
    public double AguaHigieneKgCmDia { get; set; }
    public double DescargaUrinaKgCmDia { get; set; }
    public double CalorMetabolicoW { get; set; }
    public double FatorAtividadeSono { get; set; }
    public double FatorAtividadeTrabalho { get; set; }
    public double FatorAtividadeEva { get; set; }
    public double TrajeVazamentoKgH { get; set; }

    // Equipamentos
    public double Co2RemocaoCapacidadeKgDia { get; set; }
    public double Co2RemocaoPpReferenciaKpa { get; set; }
    public double Co2RemocaoPotenciaW { get; set; }
    public double O2GeracaoMaxKgDia { get; set; }
    public double EletroliseKwhPorKgO2 { get; set; }
    public double DesumidificadorPotenciaW { get; set; }
    public double DesumidificadorCapacidadeKgH { get; set; }
    public double AquecimentoPotenciaMaxW { get; set; }
    public double UaCabineWK { get; set; }
    public double CapacidadeTermicaJK { get; set; }
    public double FracaoCalorEquipamentos { get; set; }
    public double AguaProcessadorCapacidadeKgH { get; set; }
    public double AguaRecuperacaoFracao { get; set; }
    public double AguaProcessadorPotenciaW { get; set; }
    public double AguaTempoTratamentoH { get; set; }
    public double AguaPotavelInicialKg { get; set; }
    public double AguaResidualInicialKg { get; set; }
    public double TanquePotavelCapacidadeKg { get; set; }
    public double ReservaBebidaKg { get; set; }
    public double AlimentoSecoInicialKg { get; set; }
    public double RegolitoFracaoAgua { get; set; }
    public double FornoRegolitoCapacidadeKgH { get; set; }
    public double FornoKwhPorKgAgua { get; set; }
    public double ComunicacaoPotenciaW { get; set; }
    public double IluminacaoPotenciaW { get; set; }
    public double LaboratorioPotenciaW { get; set; }
    public double RoverBateriaWh { get; set; }
    public double RoverCarregadorPotenciaW { get; set; }
    public double RoverWhPorKm { get; set; }
    public double RadiacaoSuperficieMsvDia { get; set; }
    public double VedacaoVazamentoMaxFracaoDia { get; set; }

    // Derivados do passo de tempo: 1 "hora marciana" = 1/24 de sol
    [JsonIgnore] public double PassoS => SolSegundos / 24.0;
    [JsonIgnore] public double PassoH => PassoS / 3600.0;
    [JsonIgnore] public double PassoDia => PassoS / 86400.0;

    public static string ParaPascal(string chave) =>
        string.Concat(chave.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    public static ParametrosCenario DoCatalogo(CatalogoParametros cat)
    {
        var p = new ParametrosCenario { VersaoModelo = cat.VersaoModelo };
        var props = typeof(ParametrosCenario).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.PropertyType == typeof(double) && x.CanWrite).ToDictionary(x => x.Name);
        var postos = new HashSet<string>();
        foreach (var r in cat.Parametros)
        {
            var nome = ParaPascal(r.Chave);
            if (props.TryGetValue(nome, out var prop))
            {
                if (!postos.Add(nome)) throw new InvalidOperationException($"Parâmetro repetido no catálogo: {r.Chave}");
                if (double.IsNaN(r.Valor) || double.IsInfinity(r.Valor)) throw new InvalidOperationException($"Valor inválido: {r.Chave}");
                prop.SetValue(p, r.Valor);
            }
            else if (r.Tipo != "referencia")
            {
                throw new InvalidOperationException($"O catálogo tem a chave '{r.Chave}', que o motor não conhece (marque tipo 'referencia' se for só informativa).");
            }
        }
        var faltando = props.Keys.Where(k => !postos.Contains(k)).OrderBy(k => k).ToList();
        if (faltando.Count > 0) throw new InvalidOperationException("Faltam parâmetros no catálogo: " + string.Join(", ", faltando));
        return p;
    }

    public static ParametrosCenario Padrao() => DoCatalogo(CatalogoParametros.Carregar());
}
