using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Connections;
using MissaoMarte.Web.Components;
using MissaoMarte.Web.Servicos;

// Missão Marte: a tela. O jogo inteiro (física e regras) mora em MissaoMarte.Nucleo; aqui só há a interface.
//   --rede    tablets e celulares da mesma rede também entram (sem isso, só este computador)
//   --abrir   abre o navegador quando o jogo está no ar (é o que o JOGAR.cmd da versão compilada usa)
var cultura = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;
try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { /* saída redirecionada: segue como está */ }

// Na versão compilada a pasta do programa traz o wwwroot: ela vira a raiz do conteúdo, de onde quer que o jogo
// seja aberto (duplo clique, atalho, linha de comando). Em desenvolvimento (dotnet run) vale a pasta do projeto.
string pastaDoPrograma = AppContext.BaseDirectory;
bool compilado = Directory.Exists(Path.Combine(pastaDoPrograma, "wwwroot"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = compilado ? pastaDoPrograma : null });

// Rodando direto da pasta do projeto, sem publicar, os arquivos da tela (estilos, imagens e o script do Blazor)
// precisam ser servidos também fora do modo de desenvolvimento. Na versão compilada isto não faz nada.
builder.WebHost.UseStaticWebAssets();

bool rede = args.Contains("--rede") || Environment.GetEnvironmentVariable("MISSAO_MARTE_REDE") == "1";
bool abrir = args.Contains("--abrir") && Environment.GetEnvironmentVariable("MISSAO_MARTE_SEM_NAVEGADOR") != "1";
int porta = PortaLivre(5080, 10, rede);          // se a 5080 estiver ocupada, tenta até a 5089
builder.WebHost.UseUrls($"http://{(rede ? "0.0.0.0" : "localhost")}:{porta}");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<ServicoPartida>();     // uma mesa: todos os aparelhos veem a mesma missão
builder.Services.AddScoped<Visao>();                 // o que cada aparelho está olhando
builder.Services.AddHttpClient<NasaOnline>(c => c.Timeout = TimeSpan.FromSeconds(6));

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("pt-BR").AddSupportedCultures("pt-BR").AddSupportedUICultures("pt-BR"));
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    string endereco = $"http://localhost:{porta}";
    Console.WriteLine();
    Console.WriteLine("  MISSÃO MARTE está no ar.");
    Console.WriteLine($"  Neste computador: {endereco}");
    if (rede)
        foreach (var ip in EnderecosDaRede())
            Console.WriteLine($"  Nos tablets e celulares da mesma rede: http://{ip}:{porta}");
    Console.WriteLine("  Para encerrar o jogo, feche esta janela. As missões ficam salvas na pasta \"partidas\".");
    Console.WriteLine();
    if (abrir) AbrirNavegador(endereco);
});

try
{
    app.Run();
}
catch (IOException ex) when (ex.InnerException is AddressInUseException)
{
    Console.WriteLine($"A porta {porta} já está em uso. Feche o outro Missão Marte (ou o programa que usa essa porta) e abra de novo.");
    Environment.ExitCode = 1;
}

// A primeira porta livre a partir da preferida. Se nenhuma estiver livre, devolve a preferida e o erro aparece na tela.
static int PortaLivre(int preferida, int tentativas, bool todasAsInterfaces)
{
    for (int p = preferida; p < preferida + tentativas; p++)
    {
        try
        {
            var escuta = new TcpListener(todasAsInterfaces ? IPAddress.Any : IPAddress.Loopback, p);
            escuta.Start();
            escuta.Stop();
            return p;
        }
        catch (SocketException) { /* ocupada: tenta a próxima */ }
    }
    return preferida;
}

static List<string> EnderecosDaRede()
{
    try
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString()).Distinct().ToList();
    }
    catch (NetworkInformationException) { return new List<string>(); }
}

static void AbrirNavegador(string endereco)
{
    try { Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true }); }
    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
    {
        Console.WriteLine("  Não consegui abrir o navegador sozinho: abra o endereço acima.");
    }
}
