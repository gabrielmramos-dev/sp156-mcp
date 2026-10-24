using System.Diagnostics;
using ExcelDataReader;

var lib = args[0];
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var path = Environment.GetEnvironmentVariable("SP156_XLSX") ?? throw new Exception("SP156_XLSX");
string[] need = { "Data de Abertura", "Data de Finalização", "Meio de Comunicação", "Tema", "Assunto", "Serviço", "Status", "Órgão", "Prefeitura Operacional", "Distrito" };
var status = new Dictionary<string, int>();
long rows = 0;
var sw = Stopwatch.StartNew();

void Tally(Func<int, string?> get, int[] idx)
{
    bool any = false;
    foreach (var i in idx) if (!string.IsNullOrEmpty(get(i))) { any = true; break; }
    if (!any) return;
    rows++;
    var s = get(idx[6]) ?? "(nulo)";
    status[s] = status.GetValueOrDefault(s) + 1;
}

if (lib == "sylvan")
{
    using var r = Sylvan.Data.Excel.ExcelDataReader.Create(path);
    if (!r.TryOpenWorksheet("Solicitações")) throw new Exception("aba");
    var names = Enumerable.Range(0, r.FieldCount).Select(i => r.GetName(i)).ToList();
    var idx = need.Select(n => names.IndexOf(n)).ToArray();
    while (r.Read()) { var n = r.RowFieldCount; Tally(i => i < n ? r.GetString(i) : null, idx); }
}
else
{
    var single = lib == "edr-single";
    using var fs = File.OpenRead(path);
    using var r = ExcelReaderFactory.CreateReader(fs, new ExcelReaderConfiguration { LeaveOpen = false });
    // SinglePassMode via config property (se existir) é tratado abaixo por reflexão
    while (r.Name != "Solicitações") if (!r.NextResult()) throw new Exception("aba");
    r.Read();
    var names = Enumerable.Range(0, r.FieldCount).Select(i => r.GetValue(i)?.ToString()).ToList();
    var idx = need.Select(n => names.IndexOf(n)).ToArray();
    while (r.Read()) Tally(i => r.GetValue(i)?.ToString(), idx);
}
sw.Stop();
var p = Process.GetCurrentProcess();
Console.WriteLine($"{lib}: linhas={rows} tempo={sw.Elapsed.TotalSeconds:F1}s pico_ws={p.PeakWorkingSet64 / 1048576} MiB");
foreach (var kv in status.OrderByDescending(k => k.Value)) Console.WriteLine($"  {kv.Key}={kv.Value}");
